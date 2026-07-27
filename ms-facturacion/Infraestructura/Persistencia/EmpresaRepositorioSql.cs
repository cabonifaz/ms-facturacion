using Microsoft.Data.SqlClient;
using System.Data;
using ms_facturacion.Aplicacion.Comun;
using ms_facturacion.Aplicacion.Puertos;
using ms_facturacion.Dominio;

namespace ms_facturacion.Infraestructura.Persistencia;

public sealed class EmpresaRepositorioSql(IConfiguration configuracion) : IEmpresaRepositorio
{
    private const string MensajeSinCabecera = "El procedimiento almacenado no devolvió el resultado esperado.";

    private string CadenaConexion => configuracion.GetConnectionString("MsFacturacion")
        ?? throw new InvalidOperationException("No se configuró la cadena de conexión 'MsFacturacion'.");

    public async Task<ResultadoOperacion<int>> InsertarAsync(
        string usuarioEjecutor, int idInquilino, string ruc, string razonSocial, string? nombreComercial,
        string direccion, string ubigeo, string departamento, string provincia, string distrito,
        int paisCodigo, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Empresa_Insertar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@chrRuc", ruc);
            comando.Parameters.AddWithValue("@vchRazonSocial", razonSocial);
            comando.Parameters.AddWithValue("@vchNombreComercial", (object?)nombreComercial ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchDireccion", direccion);
            comando.Parameters.AddWithValue("@chrUbigeo", ubigeo);
            comando.Parameters.AddWithValue("@vchDepartamento", departamento);
            comando.Parameters.AddWithValue("@vchProvincia", provincia);
            comando.Parameters.AddWithValue("@vchDistrito", distrito);
            comando.Parameters.AddWithValue("@intPaisCodigo", paisCodigo);
            comando.Parameters.AddWithValue("@bitActivo", activo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa"));

            return ResultadoOperacion<int>.DeExito(mensaje, idEmpresa);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<Empresa>> ObtenerAsync(int idInquilino, int idEmpresa, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Empresa_Obtener", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<Empresa>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);

            var empresa = new Empresa
            {
                IdEmpresa = lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                IdInquilino = lector.GetInt32(lector.GetOrdinal("IdInquilino")),
                Ruc = lector.GetString(lector.GetOrdinal("Ruc")),
                RazonSocial = lector.GetString(lector.GetOrdinal("RazonSocial")),
                NombreComercial = lector.IsDBNull(lector.GetOrdinal("NombreComercial")) ? null : lector.GetString(lector.GetOrdinal("NombreComercial")),
                Direccion = lector.GetString(lector.GetOrdinal("Direccion")),
                Ubigeo = lector.GetString(lector.GetOrdinal("Ubigeo")),
                Departamento = lector.GetString(lector.GetOrdinal("Departamento")),
                Provincia = lector.GetString(lector.GetOrdinal("Provincia")),
                Distrito = lector.GetString(lector.GetOrdinal("Distrito")),
                PaisCodigo = lector.GetString(lector.GetOrdinal("PaisCodigo")),
                Activo = lector.GetBoolean(lector.GetOrdinal("Activo")),
                FchCre = lector.GetDateTime(lector.GetOrdinal("FchCre")),
                FchMod = lector.IsDBNull(lector.GetOrdinal("FchMod")) ? null : lector.GetDateTime(lector.GetOrdinal("FchMod"))
            };

            return ResultadoOperacion<Empresa>.DeExito(mensaje, empresa);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<Empresa>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<ResultadoPaginado<EmpresaResumen>>> ListarAsync(
        int idInquilino, string? busqueda, int numeroPagina, int tamanoPagina, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Empresa_Listar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@vchBusqueda", (object?)busqueda ?? DBNull.Value);
            comando.Parameters.AddWithValue("@numPag", numeroPagina);
            comando.Parameters.AddWithValue("@intTamPag", tamanoPagina);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<ResultadoPaginado<EmpresaResumen>>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var totalRegistros = lector.GetInt32(lector.GetOrdinal("TotalRegistros"));
            var totalPaginas = lector.GetInt32(lector.GetOrdinal("TotalPaginas"));

            await lector.NextResultAsync(cancellationToken);
            var items = new List<EmpresaResumen>();
            while (await lector.ReadAsync(cancellationToken))
            {
                items.Add(new EmpresaResumen(
                    lector.GetInt32(lector.GetOrdinal("IdEmpresa")),
                    lector.GetString(lector.GetOrdinal("Ruc")),
                    lector.GetString(lector.GetOrdinal("RazonSocial")),
                    lector.GetString(lector.GetOrdinal("Departamento")),
                    lector.GetBoolean(lector.GetOrdinal("Activo"))));
            }

            var paginado = new ResultadoPaginado<EmpresaResumen>(totalRegistros, totalPaginas, items);
            return ResultadoOperacion<ResultadoPaginado<EmpresaResumen>>.DeExito(mensaje, paginado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<ResultadoPaginado<EmpresaResumen>>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> ActualizarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, string ruc, string razonSocial, string? nombreComercial,
        string direccion, string ubigeo, string departamento, string provincia, string distrito,
        int paisCodigo, bool activo, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Empresa_Actualizar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);
            comando.Parameters.AddWithValue("@chrRuc", ruc);
            comando.Parameters.AddWithValue("@vchRazonSocial", razonSocial);
            comando.Parameters.AddWithValue("@vchNombreComercial", (object?)nombreComercial ?? DBNull.Value);
            comando.Parameters.AddWithValue("@vchDireccion", direccion);
            comando.Parameters.AddWithValue("@chrUbigeo", ubigeo);
            comando.Parameters.AddWithValue("@vchDepartamento", departamento);
            comando.Parameters.AddWithValue("@vchProvincia", provincia);
            comando.Parameters.AddWithValue("@vchDistrito", distrito);
            comando.Parameters.AddWithValue("@intPaisCodigo", paisCodigo);
            comando.Parameters.AddWithValue("@bitActivo", activo);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idActualizado = lector.GetInt32(lector.GetOrdinal("IdEmpresa"));

            return ResultadoOperacion<int>.DeExito(mensaje, idActualizado);
        }
        catch (Exception ex)
        {
            return ResultadoOperacion<int>.DeErrorSistema(ex.Message);
        }
    }

    public async Task<ResultadoOperacion<int>> EliminarAsync(
        string usuarioEjecutor, int idInquilino, int idEmpresa, CancellationToken cancellationToken)
    {
        try
        {
            await using var conexion = new SqlConnection(CadenaConexion);
            await using var comando = new SqlCommand("SP_Empresa_Eliminar", conexion) { CommandType = CommandType.StoredProcedure };

            comando.Parameters.AddWithValue("@vchUsuarioEjecutor", usuarioEjecutor);
            comando.Parameters.AddWithValue("@intIdInquilino", idInquilino);
            comando.Parameters.AddWithValue("@intIdEmpresa", idEmpresa);

            await conexion.OpenAsync(cancellationToken);
            await using var lector = await comando.ExecuteReaderAsync(cancellationToken);

            var (idTipoMensaje, mensaje) = await LeerCabeceraAsync(lector, cancellationToken);
            if (idTipoMensaje != TipoMensaje.Exito)
            {
                return new ResultadoOperacion<int>(idTipoMensaje, mensaje, default);
            }

            await lector.NextResultAsync(cancellationToken);
            await lector.ReadAsync(cancellationToken);
            var idEliminado = lector.GetInt32(lector.GetOrdinal("IdEmpresa"));

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
