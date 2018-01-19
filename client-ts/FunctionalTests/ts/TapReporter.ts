class TapReporter implements jasmine.CustomReporter {
    constructor(private log: (message?: any, ...optionalParams: any[]) => void) {
    }

    specLog(message?: any, ...optionalParams: any[]) {

    }

    jasmineStarted?(suiteInfo: jasmine.SuiteInfo) {
        this.log("TAP version 13");
        this.log(`1..${suiteInfo.totalSpecsDefined}`);
    }

    suiteStarted?(result: jasmine.CustomReporterResult) {

    }

    specStarted?(result: jasmine.CustomReporterResult) {
        
    }
    
    specDone?(result: jasmine.CustomReporterResult) {
        
    }

    suiteDone?(result: jasmine.CustomReporterResult) {
        
    }

    jasmineDone?(runDetails: jasmine.RunDetails) {
        
    }
}

// Suppress console.log output, and send it to the reporter instead
let reporter = new TapReporter(console.log)
jasmine.getEnv().addReporter(reporter);
console.log = reporter.specLog;