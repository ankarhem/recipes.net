namespace App.Identity;

public interface IRecoveryCodeGenerator
{
    IReadOnlyList<string> Generate();
}
