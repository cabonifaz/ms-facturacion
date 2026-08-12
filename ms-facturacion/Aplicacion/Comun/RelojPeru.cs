namespace ms_facturacion.Aplicacion.Comun;

/// DateTime.Now depende de la zona horaria del sistema operativo del servidor — en Azure App Service
/// suele ser UTC por defecto, así que confiar en DateTime.Now pone la fecha del día siguiente en
/// cualquier operación hecha después de las 19:00 hora Perú (medianoche UTC), sin importar en qué región
/// o con qué configuración corra el proceso. Partir siempre de DateTime.UtcNow y convertir explícitamente
/// a America/Lima da el mismo resultado sin importar la zona horaria del host.
public static class RelojPeru
{
    private static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Lima");

    public static DateTime Ahora() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ZonaHoraria);
}
