namespace CatalogoMaui.Services;

public static class FotosProducto
{
    private const long MaxBytes = 15 * 1024 * 1024; // 15 MB
    private static readonly string[] ExtensionesPermitidas = { ".jpg", ".jpeg", ".png" };
    private static readonly HashSet<string> FotosIncluidas = new(StringComparer.OrdinalIgnoreCase)
    {
        "mochila.jpg", "libreta.jpg", "termo.jpg"
    };

    private static string CarpetaFotos =>
        Path.Combine(FileSystem.AppDataDirectory, "fotos_productos");

    public static bool EsIncluida(string? nombre) =>
        !string.IsNullOrWhiteSpace(nombre) && FotosIncluidas.Contains(nombre);

    public static string Resolver(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return "mochila.jpg";

        if (EsIncluida(nombre))
            return nombre;

        string rutaCompleta = Path.Combine(CarpetaFotos, nombre);
        return File.Exists(rutaCompleta) ? rutaCompleta : "mochila.jpg";
    }

    public static async Task<string?> ElegirAsync()
    {
        var opciones = new PickOptions
        {
            PickerTitle = "Selecciona una imagen JPG o PNG",
            FileTypes = FilePickerFileType.Images
        };

        var resultado = await FilePicker.Default.PickAsync(opciones);
        if (resultado is null)
            return null;

        string ext = Path.GetExtension(resultado.FileName).ToLowerInvariant();
        if (!ExtensionesPermitidas.Contains(ext))
            throw new InvalidOperationException("Solo se permiten imágenes JPG, JPEG o PNG.");

        await using var origen = await resultado.OpenReadAsync();
        if (origen.CanSeek && origen.Length > MaxBytes)
            throw new InvalidOperationException("La imagen no debe superar los 15 MB.");

        string rutaTemp = Path.Combine(FileSystem.CacheDirectory, $"preview_{Guid.NewGuid():N}{ext}");
        await using var destino = File.Create(rutaTemp);

        long totalLeido = 0;
        byte[] buffer = new byte[81920];
        int leidos;
        while ((leidos = await origen.ReadAsync(buffer)) > 0)
        {
            totalLeido += leidos;
            if (totalLeido > MaxBytes)
            {
                destino.Close();
                File.Delete(rutaTemp);
                throw new InvalidOperationException("La imagen no debe superar los 15 MB.");
            }
            await destino.WriteAsync(buffer.AsMemory(0, leidos));
        }

        return rutaTemp;
    }

    public static async Task<string> GuardarAsync(string rutaTemporal)
    {
        Directory.CreateDirectory(CarpetaFotos);

        string ext = Path.GetExtension(rutaTemporal).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        string nombreFinal = $"producto_{Guid.NewGuid():N}{ext}";
        string rutaFinal = Path.Combine(CarpetaFotos, nombreFinal);

        await using var origen = File.OpenRead(rutaTemporal);
        await using var destino = File.Create(rutaFinal);
        await origen.CopyToAsync(destino);

        return nombreFinal;
    }

    public static void EliminarSiEsPropia(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre) || EsIncluida(nombre))
            return;

        try
        {
            string ruta = Path.Combine(CarpetaFotos, nombre);
            if (File.Exists(ruta))
                File.Delete(ruta);
        }
        catch
        {
            // Ignorar errores al limpiar archivos huérfanos
        }
    }
}