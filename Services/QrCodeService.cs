using QRCoder;

namespace LaundryMVC.Services;

public interface IQrCodeService
{
    (string Token, string Base64Png) GenerateQr(int orderId);
}

public class QrCodeService : IQrCodeService
{
    public (string Token, string Base64Png) GenerateQr(int orderId)
    {
        var guid = Guid.NewGuid().ToString("N")[..8];
        var token = $"LMS-ORD-{orderId}-{guid}";

        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrCodeData);
        byte[] qrCodeBytes = qrCode.GetGraphic(10);

        var base64Png = $"data:image/png;base64,{Convert.ToBase64String(qrCodeBytes)}";
        return (token, base64Png);
    }
}
