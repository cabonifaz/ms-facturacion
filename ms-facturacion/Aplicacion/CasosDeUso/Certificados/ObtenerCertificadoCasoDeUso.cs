using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.Certificados;

public sealed class ObtenerCertificadoCasoDeUso(ICertificadoRepositorio repositorio)
{
    public Task<ResultadoOperacion<Certificado>> EjecutarAsync(
        int idInquilino, int idCertificado, CancellationToken cancellationToken) =>
        repositorio.ObtenerAsync(idInquilino, idCertificado, cancellationToken);
}
