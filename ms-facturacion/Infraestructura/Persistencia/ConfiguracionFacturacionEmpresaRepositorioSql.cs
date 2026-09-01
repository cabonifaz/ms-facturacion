using MySqlConnector;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class ConfiguracionFacturacionEmpresaRepositorioSql(IConfiguration configuracion) : IConfiguracionFacturacionEmpresaRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string ambienteCodigo, string tipoProveedorCodigo,
        string? nombreProveedor, int idCertificado, string? urlEnvioFacturaBoletaNota, string? urlEnvioRetencionPercepcion,
        string? urlEnvioGuiaRemision, string? urlConsultaEstadoCdr, string? urlConsultaValidez, bool activo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchAmbienteCodigo", ambienteCodigo);
            comando.Parameters.AddWithValue("@p_vchTipoProveedorCodigo", tipoProveedorCodigo);
            comando.Parameters.AddWithValue("@p_vchNombreProveedor", (object?)nombreProveedor ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdCertificado", idCertificado);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioFacturaBoletaNota", (object?)urlEnvioFacturaBoletaNota ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioRetencionPercepcion", (object?)urlEnvioRetencionPercepcion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioGuiaRemision", (object?)urlEnvioGuiaRemision ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlConsultaEstadoCdr", (object?)urlConsultaEstadoCdr ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlConsultaValidez", (object?)urlConsultaValidez ?? DBNull.Value);
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
            var idConfiguracion = lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa"));

            return ResultadoOperacion<int>.DeExito(mensaje, idConfiguracion);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ConfiguracionFacturacionEmpresa>> ObtenerAsync(
        int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdConfiguracionFacturacionEmpresa", idConfiguracionFacturacionEmpresa);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ConfiguracionFacturacionEmpresa>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var configuracion = new ConfiguracionFacturacionEmpresa
            {
                IdConfiguracionFacturacionEmpresa = lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa")),
                IdEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                AmbienteCodigo = lector.GetString(lector.GetOrdinal("AmbienteCodigo")),
                TipoProveedorCodigo = lector.GetString(lector.GetOrdinal("TipoProveedorCodigo")),
                NombreProveedor = LeerNullableString(lector, "NombreProveedor"),
                IdCertificado = lector.GetInt32(lector.GetOrdinal("IdCertificado")),
                UrlEnvioFacturaBoletaNota = LeerNullableString(lector, "UrlEnvioFacturaBoletaNota"),
                UrlEnvioRetencionPercepcion = LeerNullableString(lector, "UrlEnvioRetencionPercepcion"),
                UrlEnvioGuiaRemision = LeerNullableString(lector, "UrlEnvioGuiaRemision"),
                UrlConsultaEstadoCdr = LeerNullableString(lector, "UrlConsultaEstadoCdr"),
                UrlConsultaValidez = LeerNullableString(lector, "UrlConsultaValidez"),
                Activo = lector.GetBoolean(lector.GetOrdinal("Activo")),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre")),
                FchMod = lector.IsDBNull(lector.GetOrdinal("FchMod")) ? null : lector.GetDateTime(lector.GetOrdinal("FchMod"))
            };

            return ResultadoOperacion<ConfiguracionFacturacionEmpresa>.DeExito(mensaje, configuracion);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ConfiguracionFacturacionEmpresa>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>> ObtenerPorEmpresaYAmbienteAsync(
        int idInquilino, int idEmpresa, string ambienteCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_ObtenerPorEmpresaYAmbiente", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchAmbienteCodigo", ambienteCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var configuracion = new ConfiguracionFacturacionEmpresaPorAmbiente(
                lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa")),
                lector.GetString(lector.GetOrdinal("TipoProveedorCodigo")),
                LeerNullableString(lector, "NombreProveedor"),
                lector.GetInt32(lector.GetOrdinal("IdCertificado")),
                LeerNullableString(lector, "UrlEnvioFacturaBoletaNota"),
                LeerNullableString(lector, "UrlEnvioRetencionPercepcion"),
                LeerNullableString(lector, "UrlEnvioGuiaRemision"),
                LeerNullableString(lector, "UrlConsultaEstadoCdr"),
                LeerNullableString(lector, "UrlConsultaValidez"));

            return ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>.DeExito(mensaje, configuracion);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ConfiguracionFacturacionEmpresaPorAmbiente>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_numPag", numeroPagina);
            comando.Parameters.AddWithValue("@p_intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<ConfiguracionFacturacionEmpresaResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new ConfiguracionFacturacionEmpresaResumen(
                    lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa")),
                    lector.GetString(lector.GetOrdinal("AmbienteCodigo")),
                    lector.GetString(lector.GetOrdinal("TipoProveedorCodigo")),
                    LeerNullableString(lector, "NombreProveedor"),
                    lector.GetBoolean(lector.GetOrdinal("Activo"))));
            }

            var paginado = new ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<ConfiguracionFacturacionEmpresaResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idConfiguracionFacturacionEmpresa, string ambienteCodigo,
        string tipoProveedorCodigo, string? nombreProveedor, int idCertificado, string? urlEnvioFacturaBoletaNota,
        string? urlEnvioRetencionPercepcion, string? urlEnvioGuiaRemision, string? urlConsultaEstadoCdr,
        string? urlConsultaValidez, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdConfiguracionFacturacionEmpresa", idConfiguracionFacturacionEmpresa);
            comando.Parameters.AddWithValue("@p_vchAmbienteCodigo", ambienteCodigo);
            comando.Parameters.AddWithValue("@p_vchTipoProveedorCodigo", tipoProveedorCodigo);
            comando.Parameters.AddWithValue("@p_vchNombreProveedor", (object?)nombreProveedor ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_intIdCertificado", idCertificado);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioFacturaBoletaNota", (object?)urlEnvioFacturaBoletaNota ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioRetencionPercepcion", (object?)urlEnvioRetencionPercepcion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlEnvioGuiaRemision", (object?)urlEnvioGuiaRemision ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlConsultaEstadoCdr", (object?)urlConsultaEstadoCdr ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_vchUrlConsultaValidez", (object?)urlConsultaValidez ?? DBNull.Value);
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
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idConfiguracionFacturacionEmpresa, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_ConfiguracionFacturacionEmpresa_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdConfiguracionFacturacionEmpresa", idConfiguracionFacturacionEmpresa);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdConfiguracionFacturacionEmpresa"));

            return ResultadoOperacion<int>.DeExito(mensaje, idEliminado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    private static string? LeerNullableString(MySqlDataReader lector, string columna)
    {
        var ordinal = lector.GetOrdinal(columna);
        return lector.IsDBNull(ordinal) ? null : lector.GetString(ordinal);
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
