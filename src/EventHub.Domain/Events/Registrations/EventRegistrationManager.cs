﻿using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Identity;

namespace EventHub.Events.Registrations
{
    public class EventRegistrationManager : DomainService
    {
        private readonly IRepository<EventRegistration, Guid> _eventRegistrationRepository;

        public EventRegistrationManager(IRepository<EventRegistration, Guid> eventRegistrationRepository)
        {
            _eventRegistrationRepository = eventRegistrationRepository;
        }

        public async Task<EventRegistration> CreateAsync(Guid eventId, Guid userId)
        {
            return new EventRegistration(GuidGenerator.Create(), eventId, userId);
        }

        public async Task RegisterAsync(
            Event @event,
            IdentityUser user)
        {
            if (IsPastEvent(@event))
            {
                throw new BusinessException(EventHubErrorCodes.CantRegisterOrUnregisterForAPastEvent);
            }

            if (await IsRegisteredAsync(@event, user))
            {
                return;
            }
            
            var registrationCount = await _eventRegistrationRepository.CountAsync(x => x.EventId == @event.Id);

            if (@event.Capacity != null &&  registrationCount >= @event.Capacity)
            {
                throw new BusinessException(EventHubErrorCodes.CapacityOfEventFull)
                    .WithData("EventTitle", @event.Title);
            }
                
            await _eventRegistrationRepository.InsertAsync(
                new EventRegistration(
                    GuidGenerator.Create(),
                    @event.Id,
                    user.Id
                )
            );
        }

        public async Task UnregisterAsync(
            Event @event,
            IdentityUser user)
        {
            if (IsPastEvent(@event))
            {
                throw new BusinessException(EventHubErrorCodes.CantRegisterOrUnregisterForAPastEvent);
            }

            await _eventRegistrationRepository.DeleteAsync(
                x => x.EventId == @event.Id && x.UserId == user.Id
            );
        }

        public async Task<bool> IsRegisteredAsync(
            Event @event,
            IdentityUser user)
        {
            return await _eventRegistrationRepository
                .AnyAsync(x => x.EventId == @event.Id && x.UserId == user.Id);
        }

        public bool IsPastEvent(Event @event)
        {
            if (Clock.Now > @event.EndTime)
            {
                return true;
            }

            return false;
        }
    }
}
