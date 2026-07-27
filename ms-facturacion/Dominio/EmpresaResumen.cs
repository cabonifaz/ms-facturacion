namespace ms_facturacion.Dominio;

/// Proyección liviana para listados — SP_Empresa_Listar solo devuelve estas columnas.
public sealed record EmpresaResumen(int IdEmpresa, string Ruc, string RazonSocial, string Departamento, bool Activo);
