namespace ms_facturacion.Dominio;

/// Datos del cliente tal como los envía el llamador — no hay tabla CLIENTES, este snapshot va directo a
/// las columnas Cliente* de DOCUMENTOS_ELECTRONICOS (como Num1, no como código resuelto — lo resuelve
/// SP_DocumentoElectronico_Obtener via JOIN al leer).
/// IdTipoDocumentoSunat = Num1 de TABLA_MAESTRA IdMaestro=3 (misma numeración que
/// maximlian3_backend.IdMaestro=70, que es copia 1:1 de este catálogo).
/// PaisCodigo = Num1 de TABLA_MAESTRA IdMaestro=2 (misma numeración que maximlian3_backend, comparten
/// numeración directa a diferencia de Tipo Registro Tributario).
public sealed record ClienteDatosEntrada(
    int IdTipoDocumentoSunat, string NumeroDocumento, string? Nombre, string? Correo, string? Direccion,
    int PaisCodigo);
