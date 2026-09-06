using HomeStock.Windows.Models;

namespace HomeStock.Windows.Services;

public interface ICredentialStore
{
    StoredSession? Read();
    void Save(StoredSession session);
    void Delete();
}
