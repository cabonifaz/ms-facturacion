using System.Reflection;
using PdfSharp.Fonts;

namespace ms_facturacion.Infraestructura.Pdf;

/// PDFsharp no tiene fuentes del sistema disponibles en un contenedor Linux headless — Ubuntu-Regular/Bold
/// van embebidas como recurso del ensamblado (Infraestructura/Pdf/Fuentes/*.ttf) en vez de depender de
/// fuentes instaladas o de una descarga a S3 (a diferencia de maximlian3_backend/PdfGeneratorService,
/// que sí las trae de S3 — acá no hace falta esa complejidad, solo una familia tipográfica fija).
public sealed class FuenteEmbebidaResolver : IFontResolver
{
    public const string NombreFamilia = "Ubuntu";

    // Familia aparte para negrita, en vez de depender del flag isBold de ResolveTypeface: con una sola
    // familia "Ubuntu" + isBold, PdfSharp terminaba embebiendo un único recurso de fuente (Ubuntu Regular)
    // para toda la página — cualquier XFont con XFontStyleEx.Bold se veía en la práctica igual que el
    // regular. Pidiendo directamente la familia "Ubuntu-Bold" se evita esa resolución de estilo y el TTF
    // en negrita queda embebido de verdad.
    public const string NombreFamiliaBold = "Ubuntu-Bold";

    private static readonly Lazy<byte[]> Regular = new(() => LeerRecurso("Ubuntu-Regular.ttf"));
    private static readonly Lazy<byte[]> Bold = new(() => LeerRecurso("Ubuntu-Bold.ttf"));

    public string DefaultFontName => NombreFamilia;

    public byte[] GetFont(string faceName) => faceName switch
    {
        NombreFamiliaBold => Bold.Value,
        _ => Regular.Value
    };

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
        new(familyName == NombreFamiliaBold ? NombreFamiliaBold : NombreFamilia);

    private static byte[] LeerRecurso(string nombreArchivo)
    {
        var ensamblado = Assembly.GetExecutingAssembly();
        var nombreCompleto = ensamblado.GetManifestResourceNames()
            .First(n => n.EndsWith(nombreArchivo, StringComparison.OrdinalIgnoreCase));

        using var stream = ensamblado.GetManifestResourceStream(nombreCompleto)
            ?? throw new InvalidOperationException($"No se encontró el recurso embebido '{nombreArchivo}'.");

        using var memoria = new MemoryStream();
        stream.CopyTo(memoria);
        return memoria.ToArray();
    }
}
