using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.Empresas;

namespace ms_facturacion.Controllers;

/// PaisCodigo = Num1 de TABLA_MAESTRA IdMaestro=2 (misma numeración que ya usa maximlian3_backend para
/// identificar países), no el código ISO — se matchea por id, no por string.
public sealed record InsertarEmpresaPeticion(
    int IdInquilino, string Ruc, string RazonSocial, string? NombreComercial, string Direccion,
    string Ubigeo, string Departamento, string Provincia, string Distrito, int PaisCodigo, bool Activo);

public sealed record ActualizarEmpresaPeticion(
    string Ruc, string RazonSocial, string? NombreComercial, string Direccion,
    string Ubigeo, string Departamento, string Provincia, string Distrito, int PaisCodigo, bool Activo);

[ApiController]
[Route("api/v1/empresas")]
public sealed class EmpresasController(
    InsertarEmpresaCasoDeUso insertarCasoDeUso,
    ObtenerEmpresaCasoDeUso obtenerCasoDeUso,
    ListarEmpresasCasoDeUso listarCasoDeUso,
    ActualizarEmpresaCasoDeUso actualizarCasoDeUso,
    EliminarEmpresaCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarEmpresaPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.Ruc, peticion.RazonSocial, peticion.NombreComercial,
            peticion.Direccion, peticion.Ubigeo, peticion.Departamento, peticion.Provincia, peticion.Distrito,
            peticion.PaisCodigo, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idEmpresa:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idEmpresa, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] string? busqueda, [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, busqueda, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idEmpresa:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idEmpresa, ActualizarEmpresaPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idEmpresa, peticion.Ruc, peticion.RazonSocial, peticion.NombreComercial,
            peticion.Direccion, peticion.Ubigeo, peticion.Departamento, peticion.Provincia, peticion.Distrito,
            peticion.PaisCodigo, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idEmpresa:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idEmpresa, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idEmpresa, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
