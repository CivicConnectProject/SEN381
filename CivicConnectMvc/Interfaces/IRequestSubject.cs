using CivicConnectMvc.Models;

namespace CivicConnectMvc.Interfaces;

public interface IRequestSubject
{
    void Attach(IRequestObserver observer);
    void Detach(IRequestObserver observer);
    void Notify(ServiceRequest request, string action);
}
