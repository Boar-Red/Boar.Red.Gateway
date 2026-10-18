using Boar.Red.Service.Services;

namespace Boar.Red.Gateway.Controllers;

public class ServiceAdministratorController(ServiceAdministratorService serviceAdministratorService)
	: Boar.Red.Service.BrService.Controllers.ServiceAdministratorController(serviceAdministratorService, false)
{ }