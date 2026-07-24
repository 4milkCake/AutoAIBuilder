namespace AutoAIBuilder.Application.Projects;

public sealed class ActiveProjectContext
{
    private readonly IActiveProjectStateRepository? _repository;
    private Guid? _projectId;

    public ActiveProjectContext(IActiveProjectStateRepository? repository = null)
    {
        _repository = repository;
        _projectId = repository?.Load();
    }

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
        _repository?.Save(projectId);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        if (_projectId is null)
        {
            return;
        }

        _projectId = null;
        _repository?.Save(null);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Reload()
    {
        if (_repository is null)
        {
            return;
        }

        var projectId = _repository.Load();
        if (_projectId == projectId)
        {
            return;
        }

        _projectId = projectId;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
