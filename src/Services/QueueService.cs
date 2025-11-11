using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ZebraAirPrintService.Interfaces;
using ZebraAirPrintService.Models;
using ZebraAirPrintService.Utils;

namespace ZebraAirPrintService.Services;

/// <summary>
/// Service for managing print job queue with retry logic
/// </summary>
public class QueueService : IQueueService
{
    private readonly ILogger<QueueService> _logger;
    private readonly IPrinterService _printerService;
    private readonly ConfigManager _configManager;
    private readonly QueueConfiguration _queueConfig;

    private readonly ConcurrentQueue<PrintJob> _queue = new();
    private readonly ConcurrentDictionary<int, PrintJob> _activeJobs = new();

    private CancellationTokenSource? _processingCts;
    private Task? _processingTask;
    private int _nextJobId = 1;

    public QueueService(
        ILogger<QueueService> logger,
        IPrinterService printerService,
        ConfigManager configManager)
    {
        _logger = logger;
        _printerService = printerService;
        _configManager = configManager;
        _queueConfig = _configManager.GetQueueConfig();
    }

    /// <inheritdoc />
    public void Enqueue(PrintJob job)
    {
        // Assign job ID if not already set
        if (job.JobId == 0)
        {
            job.JobId = Interlocked.Increment(ref _nextJobId);
        }

        // Check queue capacity
        if (_queue.Count >= _queueConfig.MaxJobs)
        {
            _logger.LogWarning(
                "Queue is full ({MaxJobs} jobs). Discarding oldest job to make room for job {JobId}",
                _queueConfig.MaxJobs, job.JobId);

            // Remove oldest job
            _queue.TryDequeue(out _);
        }

        // Check if queue is getting full
        if (_queue.Count > _queueConfig.MaxJobs * 0.8)
        {
            _logger.LogWarning(
                "Queue is {Percentage}% full ({CurrentCount}/{MaxJobs})",
                (int)((_queue.Count / (double)_queueConfig.MaxJobs) * 100),
                _queue.Count,
                _queueConfig.MaxJobs);
        }

        job.Status = PrintJobStatus.Pending;
        _queue.Enqueue(job);

        _logger.LogInformation(
            "Job {JobId} added to queue. Current queue size: {QueueSize}",
            job.JobId, _queue.Count);
    }

    /// <inheritdoc />
    public PrintJob? Dequeue()
    {
        if (_queue.TryDequeue(out var job))
        {
            return job;
        }
        return null;
    }

    /// <inheritdoc />
    public async Task StartProcessingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting queue processing service");

        _processingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _processingTask = Task.Run(() => ProcessQueueAsync(_processingCts.Token), _processingCts.Token);

        await _processingTask;
    }

    /// <inheritdoc />
    public void StopProcessing()
    {
        _logger.LogInformation("Stopping queue processing service");
        _processingCts?.Cancel();
        _processingTask?.Wait(TimeSpan.FromSeconds(10));
    }

    /// <inheritdoc />
    public int GetQueueCount()
    {
        return _queue.Count + _activeJobs.Count;
    }

    /// <inheritdoc />
    public IEnumerable<PrintJob> GetJobs()
    {
        return _queue.Concat(_activeJobs.Values);
    }

    /// <summary>
    /// Main processing loop for the queue
    /// </summary>
    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Queue processing loop started");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check if printer is available
                if (!_printerService.IsPrinterAvailable())
                {
                    _logger.LogWarning("Printer is not available. Waiting before retry...");
                    await Task.Delay(TimeSpan.FromSeconds(_queueConfig.RetryIntervalSeconds), cancellationToken);
                    continue;
                }

                // Try to get next job
                if (_queue.TryDequeue(out var job))
                {
                    await ProcessJobAsync(job, cancellationToken);
                }
                else
                {
                    // No jobs in queue, wait a bit
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                // Also retry any failed jobs
                await RetryFailedJobsAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Queue processing cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processing loop: {ErrorMessage}", ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        _logger.LogInformation("Queue processing loop stopped");
    }

    /// <summary>
    /// Processes a single job
    /// </summary>
    private async Task ProcessJobAsync(PrintJob job, CancellationToken cancellationToken)
    {
        try
        {
            job.Status = PrintJobStatus.Processing;
            _activeJobs.TryAdd(job.JobId, job);

            _logger.LogInformation("Processing job {JobId} (Attempt {RetryCount})", job.JobId, job.RetryCount + 1);

            bool success = await _printerService.PrintAsync(job);

            if (success)
            {
                job.Status = PrintJobStatus.Completed;
                _logger.LogInformation("Job {JobId} completed successfully", job.JobId);

                // Remove from active jobs
                _activeJobs.TryRemove(job.JobId, out _);
            }
            else
            {
                job.Status = PrintJobStatus.Failed;
                job.RetryCount++;
                job.LastRetryAttempt = DateTime.Now;

                _logger.LogWarning(
                    "Job {JobId} failed (Attempt {RetryCount}). Will retry...",
                    job.JobId, job.RetryCount);

                // Don't remove from active jobs - will be retried
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}: {ErrorMessage}", job.JobId, ex.Message);

            job.Status = PrintJobStatus.Failed;
            job.RetryCount++;
            job.LastRetryAttempt = DateTime.Now;
            job.ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Retries failed jobs with exponential backoff
    /// </summary>
    private async Task RetryFailedJobsAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.Now;
        var jobsToRetry = _activeJobs.Values
            .Where(j => j.Status == PrintJobStatus.Failed && ShouldRetryJob(j, now))
            .ToList();

        foreach (var job in jobsToRetry)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            await ProcessJobAsync(job, cancellationToken);
        }
    }

    /// <summary>
    /// Determines if a job should be retried based on exponential backoff
    /// </summary>
    private bool ShouldRetryJob(PrintJob job, DateTime now)
    {
        if (job.LastRetryAttempt == null)
            return true;

        int delaySeconds;

        if (_queueConfig.ExponentialBackoffEnabled)
        {
            // Exponential backoff: 1s, 2s, 4s, 8s, 16s, 32s, up to MaxBackoffSeconds
            delaySeconds = Math.Min(
                (int)Math.Pow(2, job.RetryCount - 1),
                _queueConfig.MaxBackoffSeconds);
        }
        else
        {
            // Fixed interval
            delaySeconds = _queueConfig.RetryIntervalSeconds;
        }

        var timeSinceLastRetry = now - job.LastRetryAttempt.Value;
        return timeSinceLastRetry.TotalSeconds >= delaySeconds;
    }
}
