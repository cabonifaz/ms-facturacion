using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class CampoExtraDocumentoElectronicoRepositorioSql(IConfiguration configuracion) : ICampoExtraDocumentoElectronicoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CampoExtraDocumentoElectronico_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);
            comando.Parameters.AddWithValue("@vchTexto", campo.Texto);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idCampoExtra = lector.GetInt32(lector.GetOrdinal("IdCampoExtraDocumentoElectronico"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCampoExtra);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<int>>> InsertarLoteAsync(
        string usuarioEjecutor, int idInquilino, int idDocumentoElectronico, IReadOnlyList<CampoExtraEntrada> camposExtra,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CampoExtraDocumentoElectronico_InsertarLote", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);

            var tvpCamposExtra = comando.Parameters.Add("@tvpCamposExtra", SqlDbType.Structured);
            tvpCamposExtra.TypeName = "dbo.TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO";
            tvpCamposExtra.Value = ConstruirTablaCamposExtra(camposExtra);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<int>>(idTipoMensaje, mensaje, default);
            }

            var ids = new List<int>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                ids.Add(lector.GetInt32(lector.GetOrdinal("IdCampoExtraDocumentoElectronico")));
            }

            return ResultadoOperacion<IReadOnlyList<int>>.DeExito(mensaje, ids);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<int>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<IReadOnlyList<CampoExtraDocumentoElectronico>>> ListarAsync(
        int idInquilino, int idDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CampoExtraDocumentoElectronico_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdDocumentoElectronico", idDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<IReadOnlyList<CampoExtraDocumentoElectronico>>(idTipoMensaje, mensaje, default);
            }

            var camposExtra = new List<CampoExtraDocumentoElectronico>();
            await lector.NextResultAsync(cancellationToken);
            while (await lector.ReadAsync(cancellationToken))
            {
                camposExtra.Add(new CampoExtraDocumentoElectronico(
                    lector.GetInt32(lector.GetOrdinal("IdCampoExtraDocumentoElectronico")),
                    lector.GetString(lector.GetOrdinal("Texto"))));
            }

            return ResultadoOperacion<IReadOnlyList<CampoExtraDocumentoElectronico>>.DeExito(mensaje, camposExtra);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<IReadOnlyList<CampoExtraDocumentoElectronico>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CampoExtraEntrada campo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CampoExtraDocumentoElectronico_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdCampoExtraDocumentoElectronico", idCampoExtraDocumentoElectronico);
            comando.Parameters.AddWithValue("@vchTexto", campo.Texto);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idCampoExtra = lector.GetInt32(lector.GetOrdinal("IdCampoExtraDocumentoElectronico"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCampoExtra);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCampoExtraDocumentoElectronico, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_CampoExtraDocumentoElectronico_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdCampoExtraDocumentoElectronico", idCampoExtraDocumentoElectronico);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idCampoExtra = lector.GetInt32(lector.GetOrdinal("IdCampoExtraDocumentoElectronico"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCampoExtra);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    /// El orden de columnas debe coincidir exactamente con TVP_CAMPO_EXTRA_DOCUMENTO_ELECTRONICO
    /// (02_CrearTipos_MsFacturacion.sql) — una TVP basada en DataTable se mapea posicionalmente, no por nombre.
    private static DataTable ConstruirTablaCamposExtra(IReadOnlyList<CampoExtraEntrada> camposExtra)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("Texto", typeof(string));

        foreach (var campo in camposExtra)
        {
            tabla.Rows.Add(campo.Texto);
        }

        return tabla;
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        SqlDataReader lector, CancellationToken cancellationToken)
    {
        if (!await lector.ReadAsync(cancellationToken))
        {
            return (TipoMensaje.ErrorSistema, MensajeSinCabecera);
        }

        var idTipoMensaje = (TipoMensaje)lector.GetInt32(lector.GetOrdinal("IdTipoMensaje"));
        var mensaje = lector.GetString(lector.GetOrdinal("Mensaje"));
        return (idTipoMensaje, mensaje);
    }
}
