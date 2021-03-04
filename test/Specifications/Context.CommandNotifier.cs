namespace Microsoft.EntityFrameworkCore.Tests
{
    public interface ICommandNotifier
    {
        string LastCommand { get; set; }

        bool Receiving { get; }

        public void SetLastCommand(string command)
        {
            if (Receiving)
            {
                LastCommand = command;
            }
        }
    }
}
