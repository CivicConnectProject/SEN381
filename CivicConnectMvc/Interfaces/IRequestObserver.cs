using CivicConnectMvc.Models;

namespace CivicConnectMvc.Interfaces;

public interface IRequestObserver
{
    void OnRequestChanged(ServiceRequest request, string action);
}
