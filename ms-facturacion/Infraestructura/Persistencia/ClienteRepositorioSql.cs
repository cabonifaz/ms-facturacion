using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class ClienteRepositorioSql(IConfiguration configuracion) : IClienteRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, int idTipoDocumento, string numeroDocumento, string nombre,
        string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Cliente_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdTipoDocumento", idTipoDocumento);
            comando.Parameters.AddWithValue("@vchNumeroDocumento", numeroDocumento);
            comando.Parameters.AddWithValue("@vchNombre", nombre);
            comando.Parameters.AddWithValue("@vchCorreo", (object?)correo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchDireccion", (object?)direccion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intPaisCodigo", paisCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idCliente = lector.GetInt32(lector.GetOrdinal("IdCliente"));

            return ResultadoOperacion<int>.DeExito(mensaje, idCliente);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<Cliente>> ObtenerAsync(int idInquilino, int idCliente, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Cliente_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdCliente", idCliente);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<Cliente>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var cliente = new Cliente
            {
                IdCliente = lector.GetInt32(lector.GetOrdinal("IdCliente")),
                IdInquilino = lector.GetInt32(lector.GetOrdinal("IdInquilino")),
                TipoDocumentoCodigo = lector.GetString(lector.GetOrdinal("TipoDocumentoCodigo")),
                NumeroDocumento = lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                Nombre = lector.GetString(lector.GetOrdinal("Nombre")),
                Correo = lector.IsDBNull(lector.GetOrdinal("Correo")) ? null : lector.GetString(lector.GetOrdinal("Correo")),
                Direccion = lector.IsDBNull(lector.GetOrdinal("Direccion")) ? null : lector.GetString(lector.GetOrdinal("Direccion")),
                PaisCodigo = lector.GetString(lector.GetOrdinal("PaisCodigo")),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre")),
                FchMod = lector.IsDBNull(lector.GetOrdinal("FchMod")) ? null : lector.GetDateTime(lector.GetOrdinal("FchMod"))
            };

            return ResultadoOperacion<Cliente>.DeExito(mensaje, cliente);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<Cliente>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<ClienteResumen>>> ListarAsync(
        int idInquilino, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Cliente_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@numPag", numeroPagina);
            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<ClienteResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<ClienteResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new ClienteResumen(
                    lector.GetInt32(lector.GetOrdinal("IdCliente")),
                    lector.GetString(lector.GetOrdinal("NumeroDocumento")),
                    lector.GetString(lector.GetOrdinal("Nombre"))));
            }

            var paginado = new ResultadoPaginado<ClienteResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<ClienteResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<ClienteResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, int idTipoDocumento, string numeroDocumento,
        string nombre, string? correo, string? direccion, int paisCodigo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Cliente_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdCliente", idCliente);
            comando.Parameters.AddWithValue("@intIdTipoDocumento", idTipoDocumento);
            comando.Parameters.AddWithValue("@vchNumeroDocumento", numeroDocumento);
            comando.Parameters.AddWithValue("@vchNombre", nombre);
            comando.Parameters.AddWithValue("@vchCorreo", (object?)correo ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchDireccion", (object?)direccion ?? DBNull.Value);
            comando.Parameters.AddWithValue("@intPaisCodigo", paisCodigo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdCliente"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idCliente, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Cliente_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdCliente", idCliente);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdCliente"));

            return ResultadoOperacion<int>.DeExito(mensaje, idEliminado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
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
