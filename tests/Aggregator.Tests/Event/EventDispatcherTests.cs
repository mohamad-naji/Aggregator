using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aggregator.DI;
using Aggregator.Event;
using Moq;
using NUnit.Framework;

namespace Aggregator.Tests.Event
{
    [TestFixture]
    public class EventDispatcherTests
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactoryMock = new Mock<IServiceScopeFactory>();
        private readonly Mock<IServiceScope> _serviceScopeMock = new Mock<IServiceScope>();
        private readonly Mock<IEventHandler<EventA>> _eventAHandlerMock = new Mock<IEventHandler<EventA>>();
        private readonly Mock<IEventHandler<EventB>> _eventBHandlerMock = new Mock<IEventHandler<EventB>>();

        [SetUp]
        public void SetUp()
        {
            _serviceScopeFactoryMock.Reset();
            _serviceScopeMock.Reset();
            _eventAHandlerMock.Reset();
            _eventBHandlerMock.Reset();

            _serviceScopeFactoryMock.Setup(x => x.CreateScope()).Returns(_serviceScopeMock.Object);
            _serviceScopeMock
                .Setup(x => x.GetService(typeof(IEnumerable<IEventHandler<EventA>>)))
                .Returns(new[] { _eventAHandlerMock.Object });
            _serviceScopeMock
                .Setup(x => x.GetService(typeof(IEnumerable<IEventHandler<EventB>>)))
                .Returns(new[] { _eventBHandlerMock.Object });
        }

        [Test]
        public void Constructor_PassInvalidArguments_ShouldThrowException()
        {
            // Act / Assert
            var ex = Assert.Throws<ArgumentNullException>(() => new EventDispatcher<object>(null));
            Assert.That(ex.ParamName, Is.EqualTo("serviceScopeFactory"));
        }

        [Test]
        public void Dispatch_PassNullAsOrEmptyEventArray_ShouldNotThrowException()
        {
            // Arrange
            var dispatcher = new EventDispatcher<object>(_serviceScopeFactoryMock.Object);

            // Act / Assert
            Assert.DoesNotThrowAsync(() => dispatcher.Dispatch(null, default(CancellationToken)));
            Assert.DoesNotThrowAsync(() => dispatcher.Dispatch(Array.Empty<object>(), default(CancellationToken)));
        }

        [Test]
        public async Task Dispatch_EventArray_ShouldCreateServiceScope()
        {
            // Arrange
            var serviceScopeMock = new Mock<IServiceScope>();
            _serviceScopeFactoryMock
                .Setup(x => x.CreateScope())
                .Returns(serviceScopeMock.Object);
            var dispatcher = new EventDispatcher<object>(_serviceScopeFactoryMock.Object);

            // Act
            await dispatcher.Dispatch(new[] { new EventA() }, default(CancellationToken));

            // Assert
            _serviceScopeFactoryMock.Verify(x => x.CreateScope(), Times.Once);
        }

        [Test]
        public async Task Dispatch_EventArray_ShouldDisposeServiceScope()
        {
            // Arrange
            var eventHandlingScopeMock = new Mock<IServiceScope>();
            _serviceScopeFactoryMock
                .Setup(x => x.CreateScope())
                .Returns(eventHandlingScopeMock.Object);
            var dispatcher = new EventDispatcher<object>(_serviceScopeFactoryMock.Object);

            // Act
            await dispatcher.Dispatch(new[] { new EventA() }, default(CancellationToken));

            // Assert
            eventHandlingScopeMock.Verify(x => x.Dispose(), Times.Once);
        }

        [Test]
        public async Task Dispatch_SingleEvent_ShouldResolveAndInvokeSingleHandler()
        {
            // Arrange
            var singleEvent = new EventB();
            var dispatcher = new EventDispatcher<object>(_serviceScopeFactoryMock.Object);

            // Act
            await dispatcher.Dispatch(new[] { singleEvent }, default(CancellationToken));

            // Assert
            _serviceScopeMock.Verify(x => x.GetService(typeof(IEnumerable<IEventHandler<EventA>>)), Times.Never);
            _serviceScopeMock.Verify(x => x.GetService(typeof(IEnumerable<IEventHandler<EventB>>)), Times.Once);
            _eventAHandlerMock.Verify(x => x.Handle(It.IsAny<EventA>(), default(CancellationToken)), Times.Never);
            _eventBHandlerMock.Verify(x => x.Handle(It.IsAny<EventB>(), default(CancellationToken)), Times.Once);
            _eventBHandlerMock.Verify(x => x.Handle(singleEvent, default(CancellationToken)), Times.Once);
        }

        [Test]
        public async Task Dispatch_MultipleEvents_ShouldResolveAndInvokeHandlers()
        {
            // Arrange
            var eventA = new EventA();
            var eventB = new EventB();
            var dispatcher = new EventDispatcher<object>(_serviceScopeFactoryMock.Object);

            // Act
            await dispatcher.Dispatch(new object[] { eventB, eventA }, default(CancellationToken));

            // Assert
            _serviceScopeMock.Verify(x => x.GetService(typeof(IEnumerable<IEventHandler<EventA>>)), Times.Once);
            _serviceScopeMock.Verify(x => x.GetService(typeof(IEnumerable<IEventHandler<EventB>>)), Times.Once);
            _eventAHandlerMock.Verify(x => x.Handle(It.IsAny<EventA>(), default(CancellationToken)), Times.Once);
            _eventAHandlerMock.Verify(x => x.Handle(eventA, default(CancellationToken)), Times.Once);
            _eventBHandlerMock.Verify(x => x.Handle(It.IsAny<EventB>(), default(CancellationToken)), Times.Once);
            _eventBHandlerMock.Verify(x => x.Handle(eventB, default(CancellationToken)), Times.Once);
        }

        public class EventA { }

        public class EventB { }
    }
}
