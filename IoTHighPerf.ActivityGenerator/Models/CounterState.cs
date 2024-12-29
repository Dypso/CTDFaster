namespace IoTHighPerf.ActivityGenerator.Models;

public readonly record struct CounterState(
    DateOnly Date, 
    int Counter, 
    string LastGeneratedFile);
