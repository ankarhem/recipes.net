namespace App.Identity;

public interface IQrCodeGenerator
{
    byte[] GeneratePng(string content);
}
