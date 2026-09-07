namespace TaskFlow.Application.Common.Exceptions;

public sealed class ConcurrencyConflictException(Exception innerException)
    : Exception("The resource changed while this request was being processed. Reload it and review your changes before trying again.", innerException);
