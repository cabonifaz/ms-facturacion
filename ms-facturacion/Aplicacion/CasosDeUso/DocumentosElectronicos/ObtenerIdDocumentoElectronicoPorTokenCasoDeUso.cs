using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.DocumentosElectronicos;

/// Variante mínima de ObtenerDocumentoElectronicoPorTokenCasoDeUso: solo el Id + IdInquilino, para
/// que maximlian3_backend arme su propia consulta (pedidos del documento) contra su base — sin
/// idInquilino, sin autenticación de usuario, el token público es la credencial.
public sealed class ObtenerIdDocumentoElectronicoPorTokenCasoDeUso(IDocumentoElectronicoRepositorio repositorio)
{
    public Task<ResultadoOperacion<IdentificadorDocumentoPorToken>> EjecutarAsync(
        string tokenPublico, CancellationToken cancellationToken) =>
        repositorio.ObtenerIdPorTokenAsync(tokenPublico, cancellationToken);
}
