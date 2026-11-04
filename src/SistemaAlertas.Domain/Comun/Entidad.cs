namespace SistemaAlertas.Domain.Comun;

public abstract class Entidad
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreadoEn { get; protected set; } = DateTimeOffset.UtcNow;
}
