namespace AutoAIBuilder.Domain.Projects;

public static class DefaultLayerCatalog
{
    public static IReadOnlyList<LayerDefinition> Create() =>
    [
        new("ARQ-PAREDES", "Paredes", "Arquitetura", "#6F8199"),
        new("ARQ-PORTAS", "Portas", "Arquitetura", "#6F8199"),
        new("ARQ-JANELAS", "Janelas", "Arquitetura", "#6F8199"),
        new("ELE-PONTOS", "Pontos elétricos", "Elétrico", "#F8C33A"),
        new("ELE-CONDUTOS", "Condutos elétricos", "Elétrico", "#FF635C"),
        new("ELE-CIRCUITOS", "Circuitos", "Elétrico", "#4F8CFF"),
        new("HID-AGUA", "Água fria", "Hidrossanitário", "#2C9BFF"),
        new("HID-ESGOTO", "Esgoto", "Hidrossanitário", "#28C8D8"),
        new("HID-PLUVIAL", "Águas pluviais", "Hidrossanitário", "#38D1AE"),
        new("DOC-TEXTO", "Textos", "Documentação", "#91A0B6"),
        new("DOC-COTAS", "Cotas", "Documentação", "#91A0B6")
    ];
}
