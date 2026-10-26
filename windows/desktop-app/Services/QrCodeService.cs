using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace LANType.Desktop.Services;

public static class QrCodeService
{
    public static BitmapImage? GenerateQrCodeImage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        try
        {
            using var generator = new QRCodeGenerator();
            using var qrData = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            byte[] qrCodeBytes = qrCode.GetGraphic(20);

            var bitmapImage = new BitmapImage();
            using var stream = new MemoryStream(qrCodeBytes);
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            return bitmapImage;
        }
        catch
        {
            return null;
        }
    }
}
