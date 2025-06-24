/* eslint-disable @typescript-eslint/no-namespace */
/* eslint-disable @typescript-eslint/no-unsafe-return */

import { Interception } from 'cypress/types/net-stubbing';

const getManagementInfo = (cy: Cypress.cy, selector: string): any => cy.get(selector);

const getPageTestId = (selector: string): string => `[data-cy="${selector}"]`;

const getSelector = (selector: string, testId?: string): any => cy.get(testId ? getPageTestId(testId) : selector);

export const managementInfo = {
  getManagementInfo,
  getPageTestId,
  getSelector,
};

Cypress.Commands.add('getManagementInfo', () => {
  return cy.get('[data-cy="managementInfo"]');
});

declare global {
  namespace Cypress {
    interface Chainable {
      getManagementInfo(): Cypress.Chainable;
    }
  }
}

// Convert this to a module instead of script (allows import/export)
export {};
