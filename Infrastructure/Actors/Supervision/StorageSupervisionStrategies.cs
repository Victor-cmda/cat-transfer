using Akka.Actor;
using System;

namespace Infrastructure.Actors.Supervision
{
    public static class StorageSupervisionStrategies
    {
        public static SupervisorStrategy DefaultStorageStrategy => 
            new OneForOneStrategy(
                maxNrOfRetries: 3,
                withinTimeRange: TimeSpan.FromMinutes(1),
                decider: Decider.From(exception => exception switch
                {
                    System.IO.FileNotFoundException => Directive.Resume,
                    
                    System.IO.IOException => Directive.Restart,
                    
                    TimeoutException => Directive.Restart,
                    
                    ArgumentException => Directive.Resume,
                    
                    OutOfMemoryException => Directive.Escalate,
                    StackOverflowException => Directive.Escalate,
                    
                    _ => Directive.Restart
                }));

        public static SupervisorStrategy ChunkStorageStrategy =>
            new OneForOneStrategy(
                maxNrOfRetries: 5,
                withinTimeRange: TimeSpan.FromMinutes(2),
                decider: Decider.From(exception => exception switch
                {
                    System.IO.InvalidDataException => Directive.Resume,
                    
                    _ => DefaultStorageStrategy.Decider.Decide(exception)
                }));

        public static SupervisorStrategy FileRepositoryStrategy =>
            new OneForOneStrategy(
                maxNrOfRetries: 2,
                withinTimeRange: TimeSpan.FromSeconds(30),
                decider: Decider.From(exception => exception switch
                {
                    System.Data.DataException => Directive.Stop,
                    
                    _ => DefaultStorageStrategy.Decider.Decide(exception)
                }));

        public static SupervisorStrategy SystemWideStrategy =>
            new AllForOneStrategy(
                maxNrOfRetries: 2,
                withinTimeRange: TimeSpan.FromMinutes(5),
                decider: Decider.From(exception => exception switch
                {
                    OutOfMemoryException => Directive.Restart,
                    
                    _ => Directive.Escalate
                }));
    }
}
