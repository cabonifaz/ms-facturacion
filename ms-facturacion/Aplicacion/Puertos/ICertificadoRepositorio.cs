using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.Puertos;

public interface ICertificadoRepositorio
{
    Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<Certificado>> ObtenerAsync(
        int idInquilino, int idCertificado, CancellationToken cancellationToken);

    Task<ResultadoOperacion<ResultadoPaginado<CertificadoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCertificado, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken);

    Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCertificado, CancellationToken cancellationToken);
}
