using System;

namespace My.XXX.Domain.Abstractions;

public class NotConfigurationException : Exception
{
    public NotConfigurationException(string message) : base(message) { }
}
