using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.ConfiguracionesFacturacionEmpresa;

namespace ms_facturacion.Controllers;

// Deshabilitado — sin caller (ni maximlian3_backend ni ningún worker interno lo usa hoy). Comentado en vez
// de borrado para retomarlo en un sprint futuro sin tener que reescribirlo.
/*
public sealed record InsertarConfiguracionFacturacionEmpresaPeticion(
    int IdInquilino, int IdEmpresa, string AmbienteCodigo, string TipoProveedorCodigo, string? NombreProveedor,
    int IdCertificado, string? UrlEnvioFacturaBoletaNota, string? UrlEnvioRetencionPercepcion,
    string? UrlEnvioGuiaRemision, string? UrlConsultaEstadoCdr, string? UrlConsultaValidez, bool Activo);

public sealed record ActualizarConfiguracionFacturacionEmpresaPeticion(
    string AmbienteCodigo, string TipoProveedorCodigo, string? NombreProveedor, int IdCertificado,
    string? UrlEnvioFacturaBoletaNota, string? UrlEnvioRetencionPercepcion, string? UrlEnvioGuiaRemision,
    string? UrlConsultaEstadoCdr, string? UrlConsultaValidez, bool Activo);

[ApiController]
[Route("api/v1/configuraciones-facturacion-empresa")]
public sealed class ConfiguracionesFacturacionEmpresaController(
    InsertarConfiguracionFacturacionEmpresaCasoDeUso insertarCasoDeUso,
    ObtenerConfiguracionFacturacionEmpresaCasoDeUso obtenerCasoDeUso,
    ObtenerConfiguracionFacturacionEmpresaPorAmbienteCasoDeUso obtenerPorAmbienteCasoDeUso,
    ListarConfiguracionesFacturacionEmpresaCasoDeUso listarCasoDeUso,
    ActualizarConfiguracionFacturacionEmpresaCasoDeUso actualizarCasoDeUso,
    EliminarConfiguracionFacturacionEmpresaCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarConfiguracionFacturacionEmpresaPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdEmpresa, peticion.AmbienteCodigo, peticion.TipoProveedorCodigo,
            peticion.NombreProveedor, peticion.IdCertificado, peticion.UrlEnvioFacturaBoletaNota,
            peticion.UrlEnvioRetencionPercepcion, peticion.UrlEnvioGuiaRemision, peticion.UrlConsultaEstadoCdr,
            peticion.UrlConsultaValidez, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("{idConfiguracionFacturacionEmpresa:int}")]
    public async Task<IActionResult> Obtener(
        [FromQuery] int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken)
    {
        var resultado = await obtenerCasoDeUso.EjecutarAsync(idInquilino, idConfiguracionFacturacionEmpresa, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet("por-ambiente")]
    public async Task<IActionResult> ObtenerPorAmbiente(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] string ambienteCodigo, CancellationToken cancellationToken)
    {
        var resultado = await obtenerPorAmbienteCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, ambienteCodigo, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] int idEmpresa, [FromQuery] int pagina = 1,
        [FromQuery] int tamanoPagina = 20, CancellationToken cancellationToken = default)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, idEmpresa, pagina, tamanoPagina, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idConfiguracionFacturacionEmpresa:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idConfiguracionFacturacionEmpresa,
        ActualizarConfiguracionFacturacionEmpresaPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idConfiguracionFacturacionEmpresa, peticion.AmbienteCodigo,
            peticion.TipoProveedorCodigo, peticion.NombreProveedor, peticion.IdCertificado,
            peticion.UrlEnvioFacturaBoletaNota, peticion.UrlEnvioRetencionPercepcion, peticion.UrlEnvioGuiaRemision,
            peticion.UrlConsultaEstadoCdr, peticion.UrlConsultaValidez, peticion.Activo, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idConfiguracionFacturacionEmpresa:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idConfiguracionFacturacionEmpresa, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
*/
