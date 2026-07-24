namespace AutoAIBuilder.Application.Projects;

public sealed class ActiveProjectContext
{
    private Guid? _projectId;

    public event EventHandler? Changed;

    public Guid? ProjectId => _projectId;

    public void Select(Guid projectId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("O identificador do projeto ativo é inválido.", nameof(projectId));
        }

        if (_projectId == projectId)
        {
            return;
        }

        _projectId = projectId;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_projectId is null)
        {
            return;
        }

        _projectId = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
