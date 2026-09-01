using MySqlConnector;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class CertificadoRepositorioSql(IConfiguration configuracion) : ICertificadoRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Certificado_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchRutaAlmacenamiento", rutaAlmacenamiento);
            comando.Parameters.AddWithValue("@p_vchSujeto", sujeto);
            comando.Parameters.AddWithValue("@p_vchEmisor", emisor);
            comando.Parameters.AddWithValue("@p_vchNumeroSerie", numeroSerie);
            comando.Parameters.AddWithValue("@p_vchHuellaDigital", huellaDigital);
            comando.Parameters.AddWithValue("@p_dtValidoDesde", validoDesde.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@p_dtValidoHasta", validoHasta.ToDateTime(TimeOnly.MinValue));
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
            var idCertificado = lector.GetInt32(lector.GetOrdinal("IdCertificado"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCertificado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<Certificado>> ObtenerAsync(int idInquilino, int idCertificado, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Certificado_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCertificado", idCertificado);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<Certificado>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var certificado = new Certificado
            {
                IdCertificado = lector.GetInt32(lector.GetOrdinal("IdCertificado")),
                IdEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                RutaAlmacenamiento = lector.GetString(lector.GetOrdinal("RutaAlmacenamiento")),
                Sujeto = lector.GetString(lector.GetOrdinal("Sujeto")),
                Emisor = lector.GetString(lector.GetOrdinal("Emisor")),
                NumeroSerie = lector.GetString(lector.GetOrdinal("NumeroSerie")),
                HuellaDigital = lector.GetString(lector.GetOrdinal("HuellaDigital")),
                ValidoDesde = DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("ValidoDesde"))),
                ValidoHasta = DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("ValidoHasta"))),
                Activo = lector.GetBoolean(lector.GetOrdinal("Activo")),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre")),
                FchMod = lector.IsDBNull(lector.GetOrdinal("FchMod")) ? null : lector.GetDateTime(lector.GetOrdinal("FchMod"))
            };

            return ResultadoOperacion<Certificado>.DeExito(mensaje, certificado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<Certificado>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<CertificadoResumen>>> ListarAsync(
        int idInquilino, int idEmpresa, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Certificado_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@p_vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@p_numPag", numeroPagina);
            comando.Parameters.AddWithValue("@p_intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<CertificadoResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<CertificadoResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new CertificadoResumen(
                    lector.GetInt32(lector.GetOrdinal("IdCertificado")),
                    lector.GetString(lector.GetOrdinal("Sujeto")),
                    lector.GetString(lector.GetOrdinal("NumeroSerie")),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("ValidoDesde"))),
                    DateOnly.FromDateTime(lector.GetDateTime(lector.GetOrdinal("ValidoHasta"))),
                    lector.GetBoolean(lector.GetOrdinal("Activo"))));
            }

            var paginado = new ResultadoPaginado<CertificadoResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<CertificadoResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<CertificadoResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCertificado, string rutaAlmacenamiento, string sujeto, string emisor,
        string numeroSerie, string huellaDigital, DateOnly validoDesde, DateOnly validoHasta, bool activo,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Certificado_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCertificado", idCertificado);
            comando.Parameters.AddWithValue("@p_vchRutaAlmacenamiento", rutaAlmacenamiento);
            comando.Parameters.AddWithValue("@p_vchSujeto", sujeto);
            comando.Parameters.AddWithValue("@p_vchEmisor", emisor);
            comando.Parameters.AddWithValue("@p_vchNumeroSerie", numeroSerie);
            comando.Parameters.AddWithValue("@p_vchHuellaDigital", huellaDigital);
            comando.Parameters.AddWithValue("@p_dtValidoDesde", validoDesde.ToDateTime(TimeOnly.MinValue));
            comando.Parameters.AddWithValue("@p_dtValidoHasta", validoHasta.ToDateTime(TimeOnly.MinValue));
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
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdCertificado"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCertificado, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new MySqlConnection(CadenaConexion);
            await using var comando = new MySqlCommand("SP_Certificado_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@p_vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@p_intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@p_intIdCertificado", idCertificado);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdCertificado"));

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
