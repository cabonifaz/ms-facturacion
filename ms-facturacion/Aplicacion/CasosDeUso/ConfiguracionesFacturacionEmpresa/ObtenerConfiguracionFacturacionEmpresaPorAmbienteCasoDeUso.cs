using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

/// Uso principal del Worker (Módulo 4): resolver URLs SUNAT/certificado vigentes de una empresa+ambiente.
public sealed class ObtenerConfiguracionFacturacionEmpresaPorAmbienteCasoDeUso(IConfiguracionFacturacionEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>> EjecutarAsync(
        int idInquilino, int idEmpresa, string ambienteCodigo, CancellationToken cancellationToken) =>
        repositorio.ObtenerPorEmpresaYAmbienteAsync(idInquilino, idEmpresa, ambienteCodigo, cancellationToken);
}
