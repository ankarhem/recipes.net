using App.Identity;
using App.Identity.Ports;
using QRCoder;

namespace Infrastructure.Identity;

public sealed class QrCoderGenerator : IQrCodeGenerator
{
    public byte[] GeneratePng(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(qrData);
        return qr.GetGraphic(pixelsPerModule: 10);
    }
}
