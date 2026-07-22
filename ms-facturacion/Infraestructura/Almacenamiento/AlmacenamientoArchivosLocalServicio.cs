using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Infraestructura.Almacenamiento;

/// Disco local bajo una carpeta configurable — mismo espíritu que CERTIFICADOS.RutaAlmacenamiento.
/// Reemplazar por un adaptador de blob storage más adelante sin tocar quien consume el puerto.
public sealed class AlmacenamientoArchivosLocalServicio(IConfiguration configuracion) : IAlmacenamientoArchivosServicio
{
    private string RutaBase => configuracion["Almacenamiento:RutaBase"]
        ?? throw new InvalidOperationException("No se configuró 'Almacenamiento:RutaBase'.");

    public async Task<string> GuardarAsync(string nombreArchivo, byte[] contenido, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(RutaBase);
        var rutaCompleta = Path.Combine(RutaBase, nombreArchivo);
        await File.WriteAllBytesAsync(rutaCompleta, contenido, cancellationToken);
        return rutaCompleta;
    }
}
