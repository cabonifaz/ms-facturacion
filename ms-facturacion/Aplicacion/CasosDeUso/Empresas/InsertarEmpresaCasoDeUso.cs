using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;

namespace ms_facturacion.Aplicacion.CasosDeUso.Empresas;

public sealed class InsertarEmpresaCasoDeUso(IEmpresaRepositorio repositorio)
{
    public Task<ResultadoOperacion<int>> EjecutarAsync(
        string usuarioEjecutor, int idInquilino, string ruc, string razonSocial, string? nombreComercial,
        string direccion, string ubigeo, string departamento, string provincia, string distrito,
        string paisCodigo, bool activo, CancellationToken cancellationToken) =>
        repositorio.InsertarAsync(
            usuarioEjecutor, idInquilino, ruc, razonSocial, nombreComercial,
            direccion, ubigeo, departamento, provincia, distrito, paisCodigo, activo, cancellationToken);
}
