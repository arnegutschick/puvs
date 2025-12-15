namespace Chat.Contracts
{
    // Request vom Client an den Server
    public class TimeRequest
    {
        // optional: hier könnte man z.B. Zeitzone mitgeben
    }

    // Response vom Server an den Client
    public class TimeResponse
    {
        public DateTime CurrentTime { get; set; }

        public TimeResponse() { }

        public TimeResponse(DateTime currentTime)
        {
            CurrentTime = currentTime;
        }
    }
}
