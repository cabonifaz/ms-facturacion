namespace ms_facturacion.Dominio;

/// Una fila de CAMPOS_EXTRA_DOCUMENTO_ELECTRONICO — texto libre que el usuario agrega a un documento, sin
/// relación con el esquema SUNAT.
public sealed record CampoExtraDocumentoElectronico(int IdCampoExtraDocumentoElectronico, string Texto);

/// Entrada de un campo extra — usada tanto para Insertar/InsertarLote (sin Id) como para "Guardar cambios"
/// en lote (mismo criterio que LineaDocumentoElectronicoEntrada): IdCampoExtraDocumentoElectronico = 0 →
/// fila nueva, > 0 → fila existente a actualizar. Lo que no venga en el arreglo se da de baja.
public sealed record CampoExtraEntrada(string Texto, int IdCampoExtraDocumentoElectronico = 0);
