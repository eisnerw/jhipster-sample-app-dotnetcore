describe('Birthday entity list', () => {
  const username = Cypress.env('E2E_USERNAME') ?? 'user';
  const password = Cypress.env('E2E_PASSWORD') ?? 'user';

  beforeEach(() => {
    cy.login(username, password);
    cy.intercept('GET', '/api/entity').as('entityDirectory');
    cy.intercept('GET', '/api/entity/birthday/spec').as('birthdaySpec');
    cy.intercept('GET', '/api/entity/birthday/query-builder-spec').as('birthdayQbSpec');
    cy.intercept('GET', '/api/entity/birthday/search/lucene*').as('birthdaySearch');
  });

  it('loads the birthday list view', () => {
    cy.visit('');
    cy.wait('@entityDirectory');

    cy.get('[data-cy="entity"]').click();
    cy.contains('a.dropdown-item', 'Birthday').click();

    cy.url().should('include', '/entity/birthday');

    cy.wait('@birthdaySpec').its('response.statusCode').should('eq', 200);
    cy.wait('@birthdayQbSpec').its('response.statusCode').should('eq', 200);
    cy.wait('@birthdaySearch').then(({ response }) => {
      expect(response?.statusCode).to.equal(200);
      expect(response?.body?.hits).to.be.an('array');
    });

    cy.contains('h2', 'Birthdays').should('be.visible');
    cy.contains('.p-datatable-thead th', 'Last Name').should('be.visible');
    cy.contains('.p-datatable-thead th', 'First Name').should('be.visible');
    cy.contains('.p-datatable-thead th', 'Birthday').should('be.visible');
    cy.contains('.p-datatable-thead th', 'Sign').should('be.visible');
    cy.contains('.p-datatable-thead th', 'Alive?').should('be.visible');

    cy.get('.p-datatable-table').should('be.visible');
    cy.get('body', { timeout: 60000 }).should(() => {
      const loadingText = Cypress.$('tr.loading-row td').text().trim();
      const matchesLimitMessage = /^(Over 10000|\d+) hits \(too many to display, showing the first 1000\)$/.test(
        loadingText
      );
      const hasRows = Cypress.$('.p-datatable-tbody tr').length > 0;
      expect(matchesLimitMessage || hasRows, 'loading message or rows').to.be.true;
    });
  });
});
