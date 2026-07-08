using System.Threading.Tasks;

internal interface IPackageManager
{
    public Task UpdateAsync();

    public Task InstallPackageAsync(string packageName);

    public Task UpdatePackageAsync(string packageName);

    public Task UninstallPackageAsync(string packageName);
}
