using System.Globalization;

namespace App.Crawler;

public sealed record WorkflowId
{
    public string Value { get; }

    public WorkflowId(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Workflow ID cannot be empty.", nameof(value));
        }

        if (value.Length > 255)
        {
            throw new ArgumentException(
                $"Workflow ID cannot exceed 255 characters (got {value.Length}).",
                nameof(value)
            );
        }

        Value = value;
    }

    public static WorkflowId Create(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Host cannot be empty.", nameof(host));
        }

        return new WorkflowId($"{host}-{NanoidDotNet.Nanoid.Generate(size: 10)}");
    }

    public override string ToString() => Value;

    public static explicit operator string(WorkflowId id) => id.Value;
}
