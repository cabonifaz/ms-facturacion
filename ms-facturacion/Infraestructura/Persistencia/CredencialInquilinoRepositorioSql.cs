using MySqlConnector;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class CredencialInquilinoRepositorioSql(
    IConfiguration configuracion, ILogger<CredencialInquilinoRepositorioSql> logger) : ICredencialInquilinoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string tipoCredencialCodigo, string usuario,
        byte[] valorCifrado, byte[] nonce, byte[] tag, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchTipoCredencialCodigo", tipoCredencialCodigo);
            comando.Parameters.AddWithValue("@p_vchUsuario", usuario);
            comando.Parameters.Add("@p_binValorCifrado", MySqlDbType.VarBinary).Value = valorCifrado;
            comando.Parameters.Add("@p_binNonce", MySqlDbType.VarBinary, 12).Value = nonce;
            comando.Parameters.Add("@p_binTag", MySqlDbType.VarBinary, 16).Value = tag;
            comando.Parameters.AddWithValue("@p_bitActivo", activo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idCredencial = lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCredencial);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<CredencialInquilinoDetalle>> ObtenerAsync(
        int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCredencialInquilino", idCredencialInquilino);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<CredencialInquilinoDetalle>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var detalle = new CredencialInquilinoDetalle(
                lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino")),
                lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                lector.GetString(lector.GetOrdinal("TipoCredencialCodigo")),
                lector.GetString(lector.GetOrdinal("Usuario")),
                (byte[])lector["ValorCifrado"],
                (byte[])lector["Nonce"],
                (byte[])lector["Tag"],
                lector.GetBoolean(lector.GetOrdinal("Activo")),
                lector.IsDBNull(lector.GetOrdinal("FchRotacion")) ? null : lector.GetDateTime(lector.GetOrdinal("FchRotacion")));

            return ResultadoOperacion<CredencialInquilinoDetalle>.DeExito(mensaje, detalle);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<CredencialInquilinoDetalle>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<CredencialInquilinoCifrada>> ObtenerPorTipoAsync(
        int idInquilino, int idEmpresa, string tipoCredencialCodigo, CancellationToken cancellationToken)
    {
        // Log de los parámetros exactos que le llegan a SP_CredencialInquilino_ObtenerPorTipo — el WHERE de
        // ese SP exige coincidencia exacta de IdInquilino + IdEmpresa + TipoCredencialCodigo + Activo=1 +
        // SoftDelete=0; si "no encuentra" la credencial pese a existir una fila, lo más probable es que uno
        // de estos tres valores (sobre todo TipoCredencialCodigo, texto libre) no coincida exactamente con
        // lo que hay en CREDENCIALES_INQUILINO — esto lo deja explícito en vez de tener que asumirlo.
        // LogWarning, no LogInformation: Preprod tiene Logging__LogLevel__Default en un nivel que filtra
        // Information (se vio en el log anterior que solo pasaban las líneas warn:) — esta línea necesita
        // verse ahí sin tener que tocar esa configuración de Azure aparte.
        logger.LogWarning(
            "CredencialInquilino.ObtenerPorTipo — buscando. idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, tipoCredencialCodigo='{TipoCredencialCodigo}' (longitud={Longitud}).",
            idInquilino, idEmpresa, tipoCredencialCodigo, tipoCredencialCodigo.Length);

        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_ObtenerPorTipo", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchTipoCredencialCodigo", tipoCredencialCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                logger.LogWarning(
                    "CredencialInquilino.ObtenerPorTipo — el SP no encontró la credencial. idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, tipoCredencialCodigo='{TipoCredencialCodigo}': {Mensaje}",
                    idInquilino, idEmpresa, tipoCredencialCodigo, mensaje);
                return new ResultadoOperacion<CredencialInquilinoCifrada>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var credencial = new CredencialInquilinoCifrada(
                lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino")),
                lector.GetString(lector.GetOrdinal("Usuario")),
                (byte[])lector["ValorCifrado"],
                (byte[])lector["Nonce"],
                (byte[])lector["Tag"]);

            logger.LogWarning(
                "CredencialInquilino.ObtenerPorTipo — encontrada. idCredencialInquilino={IdCredencialInquilino}, usuario='{Usuario}', valorCifradoBytes={ValorCifradoBytes}.",
                credencial.IdCredencialInquilino, credencial.Usuario, credencial.ValorCifrado.Length);

            return ResultadoOperacion<CredencialInquilinoCifrada>.DeExito(mensaje, credencial);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex, "CredencialInquilino.ObtenerPorTipo — excepción no controlada. idInquilino={IdInquilino}, idEmpresa={IdEmpresa}, tipoCredencialCodigo='{TipoCredencialCodigo}'.",
                idInquilino, idEmpresa, tipoCredencialCodigo);
            return ResultadoOperacion<CredencialInquilinoCifrada>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_numPag", numeroPagina);
            comando.Parameters.AddWithValue("@p_intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<CredencialInquilinoResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new CredencialInquilinoResumen(
                    lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino")),
                    lector.GetString(lector.GetOrdinal("TipoCredencialCodigo")),
                    lector.GetString(lector.GetOrdinal("Usuario")),
                    lector.GetBoolean(lector.GetOrdinal("Activo")),
                    lector.IsDBNull(lector.GetOrdinal("FchRotacion")) ? null : lector.GetDateTime(lector.GetOrdinal("FchRotacion"))));
            }

            var paginado = new ResultadoPaginado<CredencialInquilinoResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<CredencialInquilinoResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, string usuario,
        byte[] valorCifrado, byte[] nonce, byte[] tag, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCredencialInquilino", idCredencialInquilino);
            comando.Parameters.AddWithValue("@p_vchUsuario", usuario);
            comando.Parameters.Add("@p_binValorCifrado", MySqlDbType.VarBinary).Value = valorCifrado;
            comando.Parameters.Add("@p_binNonce", MySqlDbType.VarBinary, 12).Value = nonce;
            comando.Parameters.Add("@p_binTag", MySqlDbType.VarBinary, 16).Value = tag;
            comando.Parameters.AddWithValue("@p_bitActivo", activo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCredencialInquilino, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_CredencialInquilino_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCredencialInquilino", idCredencialInquilino);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdCredencialInquilino"));

            return ResultadoOperacion<int>.DeExito(mensaje, idEliminado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    private static async Task<(TipoMensaje IdTipoMensaje, string Mensaje)> LeerCabeceraAsync(
        MySqlDataReader lector, CancellationToken cancellationToken)
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
