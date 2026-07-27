namespace ms_facturacion.Aplicacion.Comun;

/// Refleja el envelope de respuesta que devuelve todo SP: IdTipoMensaje/Mensaje como cabecera fija.
public enum TipoMensaje
{
    ReglaDeNegocio = 1,
    Exito = 2,
    ErrorSistema = 3
}

public sealed record ResultadoOperacion<T>(TipoMensaje IdTipoMensaje, string Mensaje, T? Datos)
{
    public static ResultadoOperacion<T> DeExito(string mensaje, T datos) =>
        new(TipoMensaje.Exito, mensaje, datos);

    public static ResultadoOperacion<T> DeReglaDeNegocio(string mensaje) =>
        new(TipoMensaje.ReglaDeNegocio, mensaje, default);

    public static ResultadoOperacion<T> DeErrorSistema(string mensaje) =>
        new(TipoMensaje.ErrorSistema, mensaje, default);
}
