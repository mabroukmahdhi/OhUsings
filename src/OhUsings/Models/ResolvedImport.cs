namespace OhUsings.Models
{
    /// <summary>
    /// A user-resolved ambiguous import: the type name and the namespace the user picked.
    /// </summary>
    public sealed class ResolvedImport
    {
        public string TypeName { get; }
        public string Namespace { get; }

        public ResolvedImport(string typeName, string ns)
        {
            TypeName = typeName;
            Namespace = ns;
        }
    }
}
