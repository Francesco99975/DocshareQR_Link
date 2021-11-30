using System.Threading.Tasks;
using Coravel.Invocable;
using docshareqr_link.Interfaces;

namespace docshareqr_link.Helpers
{
    public class GroupCleanerRoutine : IInvocable
    {
        private readonly IDocGroupRepository _docGroupRepository;
        public GroupCleanerRoutine(IDocGroupRepository docGroupRepository)
        {
            _docGroupRepository = docGroupRepository;
        }

        public async Task Invoke()
        {
            var deprecatedGroups = await _docGroupRepository.GetDeprecatedGroups();

            foreach (var group in deprecatedGroups)
            {
                _docGroupRepository.RemoveGroup(group);
            }

            await _docGroupRepository.SaveAllAsync();
        }
    }
}