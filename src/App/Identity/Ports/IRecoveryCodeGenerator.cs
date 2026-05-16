namespace App.Identity.Ports;

public interface IRecoveryCodeGenerator
{
    IReadOnlyList<string> Generate();
}
