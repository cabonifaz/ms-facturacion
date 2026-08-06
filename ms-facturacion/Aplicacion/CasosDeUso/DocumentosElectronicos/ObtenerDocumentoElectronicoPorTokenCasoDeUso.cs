using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Puerta de entrada de la verificación pública: dado solo el token del PDF (sin idInquilino, sin
/// autenticación de usuario). SP_DocumentoElectronico_ObtenerPorToken ya devuelve una proyección
/// público-segura (sin Id* internos), no reutiliza ObtenerAsync.
public sealed class ObtenerDocumentoElectronicoPorTokenCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<DocumentoElectronicoDetallePublico>> EjecutarAsync(
        string tokenPublico, CancellationToken cancellationToken) =>
        repositorio.ObtenerPorTokenAsync(tokenPublico, cancellationToken);
}
