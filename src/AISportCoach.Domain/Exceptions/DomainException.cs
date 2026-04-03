namespace AISportCoach.Domain.Exceptions;
public class DomainException(string message) : Exception(message);
public class VideoTooLargeException(long sizeBytes, long maxBytes)
    : DomainException($"Video size {sizeBytes} bytes exceeds maximum allowed {maxBytes} bytes.");
public class UnsupportedVideoFormatException(string extension)
    : DomainException($"Video format '{extension}' is not supported.");
public class VideoNotFoundException(Guid id)
    : DomainException($"Video with ID '{id}' was not found.");
public class ReportNotFoundException(Guid id)
    : DomainException($"Coaching report with ID '{id}' was not found.");
