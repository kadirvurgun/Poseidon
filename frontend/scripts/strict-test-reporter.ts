import type { Reporter, TestCase } from 'vitest/node'

// Inverted assertions must not count as passing release-gate evidence.
export default class StrictTestReporter implements Reporter {
  onTestCaseResult(testCase: TestCase) {
    if (testCase.options.fails) {
      throw new Error('Expected-failure tests are not accepted by the POSEIDON gate.')
    }
  }
}
