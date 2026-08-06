using Microsoft.AspNetCore.Mvc;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.CasosDeUso.CamposExtraDocumentoElectronico;
using ms_facturacion.Dominio;

namespace ms_facturacion.Controllers;

public sealed record CampoExtraPeticion(string Etiqueta, string Valor);
public sealed record InsertarCampoExtraPeticion(int IdInquilino, int IdDocumentoElectronico, string Etiqueta, string Valor);
public sealed record InsertarLoteCamposExtraPeticion(int IdInquilino, int IdDocumentoElectronico, IReadOnlyList<CampoExtraPeticion> CamposExtra);

[ApiController]
[Route("api/v1/campos-extra")]
public sealed class CamposExtraDocumentoElectronicoController(
    InsertarCampoExtraDocumentoElectronicoCasoDeUso insertarCasoDeUso,
    InsertarLoteCamposExtraDocumentoElectronicoCasoDeUso insertarLoteCasoDeUso,
    ListarCamposExtraDocumentoElectronicoCasoDeUso listarCasoDeUso,
    ActualizarCampoExtraDocumentoElectronicoCasoDeUso actualizarCasoDeUso,
    EliminarCampoExtraDocumentoElectronicoCasoDeUso eliminarCasoDeUso) : ControllerBase
{
    // TODO: reemplazar por el usuario ejecutor real una vez definida la autenticación servicio-a-servicio con maximlian3_backend.
    private const string UsuarioEjecutor = "ms-facturacion";

    [HttpPost]
    public async Task<IActionResult> Insertar(InsertarCampoExtraPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await insertarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdDocumentoElectronico,
            new CampoExtraEntrada(peticion.Etiqueta, peticion.Valor), cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpPost("lote")]
    public async Task<IActionResult> InsertarLote(InsertarLoteCamposExtraPeticion peticion, CancellationToken cancellationToken)
    {
        var camposExtra = peticion.CamposExtra.Select(c => new CampoExtraEntrada(c.Etiqueta, c.Valor)).ToList();

        var resultado = await insertarLoteCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, peticion.IdInquilino, peticion.IdDocumentoElectronico, camposExtra, cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int idInquilino, [FromQuery] int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await listarCasoDeUso.EjecutarAsync(idInquilino, idDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    [HttpPut("{idCampoExtraDocumentoElectronico:int}")]
    public async Task<IActionResult> Actualizar(
        [FromQuery] int idInquilino, int idCampoExtraDocumentoElectronico, CampoExtraPeticion peticion, CancellationToken cancellationToken)
    {
        var resultado = await actualizarCasoDeUso.EjecutarAsync(
            UsuarioEjecutor, idInquilino, idCampoExtraDocumentoElectronico,
            new CampoExtraEntrada(peticion.Etiqueta, peticion.Valor), cancellationToken);

        return ResponderSegunEnvelope(resultado);
    }

    [HttpDelete("{idCampoExtraDocumentoElectronico:int}")]
    public async Task<IActionResult> Eliminar(
        [FromQuery] int idInquilino, int idCampoExtraDocumentoElectronico, CancellationToken cancellationToken)
    {
        var resultado = await eliminarCasoDeUso.EjecutarAsync(UsuarioEjecutor, idInquilino, idCampoExtraDocumentoElectronico, cancellationToken);
        return ResponderSegunEnvelope(resultado);
    }

    private IActionResult ResponderSegunEnvelope<T>(ResultadoOperacion<T> resultado) => resultado.IdTipoMensaje switch
    {
        TipoMensaje.Exito => Ok(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje, Datos = resultado.Datos }),
        TipoMensaje.ReglaDeNegocio => BadRequest(new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje }),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new { IdTipoMensaje = (int)resultado.IdTipoMensaje, resultado.Mensaje })
    };
}
