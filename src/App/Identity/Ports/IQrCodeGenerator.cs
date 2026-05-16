namespace App.Identity.Ports;

public interface IQrCodeGenerator
{
    byte[] GeneratePng(string content);
}
