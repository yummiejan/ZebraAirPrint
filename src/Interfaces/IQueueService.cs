using ZebraAirPrintService.Models;

namespace ZebraAirPrintService.Interfaces;

/// <summary>
/// Interface for print job queue management
/// </summary>
public interface IQueueService
{
    /// <summary>
    /// Adds a job to the queue
    /// </summary>
    /// <param name="job">The job to add</param>
    void Enqueue(PrintJob job);

    /// <summary>
    /// Gets the next job from the queue
    /// </summary>
    /// <returns>The next job, or null if queue is empty</returns>
    PrintJob? Dequeue();

    /// <summary>
    /// Starts processing jobs from the queue
    /// </summary>
    Task StartProcessingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stops processing jobs
    /// </summary>
    void StopProcessing();

    /// <summary>
    /// Gets the current number of jobs in the queue
    /// </summary>
    int GetQueueCount();

    /// <summary>
    /// Gets all jobs currently in the queue (for monitoring)
    /// </summary>
    IEnumerable<PrintJob> GetJobs();
}
