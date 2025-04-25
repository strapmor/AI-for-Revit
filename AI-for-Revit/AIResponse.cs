using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using System.Collections.Generic;
using System.Linq;

namespace AI_for_Revit
{
    public enum ResponseStatus
    {
        Success,
        Error,
        InProgress,
        Warning
    }

    public class McpResponseItem
    {
        public string Tool { get; set; } = string.Empty;
        public ResponseStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public CallToolResponse? Result { get; set; }
    }

    public class AIResponse
    {
        public string Answer { get; set; } = string.Empty;
        public double Cost { get; set; }
        public string? ErrorMessage { get; set; }
        public List<McpResponseItem> McpResponses { get; set; } = new List<McpResponseItem>();
        public bool IsPartial { get; set; }
        public ResponseStatus OverallStatus 
        { 
            get 
            {
                if (McpResponses == null || McpResponses.Count == 0)
                    return string.IsNullOrEmpty(ErrorMessage) ? ResponseStatus.Success : ResponseStatus.Error;
                
                if (McpResponses.Any(r => r.Status == ResponseStatus.Error))
                    return ResponseStatus.Error;
                
                if (McpResponses.Any(r => r.Status == ResponseStatus.Warning))
                    return ResponseStatus.Warning;
                
                return ResponseStatus.Success;
            }
        }
    }
}
