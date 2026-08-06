namespace ms_facturacion.Dominio;

/// Una fila de CAMPOS_EXTRA_DOCUMENTO_ELECTRONICO — pares etiqueta/valor libres que el usuario agrega a un
/// documento, sin relación con el esquema SUNAT.
public sealed record CampoExtraDocumentoElectronico(int IdCampoExtraDocumentoElectronico, string Etiqueta, string Valor);

/// Entrada de un campo extra a insertar/actualizar — sin Id (se asigna en el INSERT o ya se conoce por
/// separado en un Actualizar).
public sealed record CampoExtraEntrada(string Etiqueta, string Valor);
