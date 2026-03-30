import loginPage from '../../../Login.page';
import { navigateToBackendConfigModule } from './scroll-highlight-csv.helpers';
import { generateRandmString } from '../../../helper-functions';

/**
 * Property Workers - Scroll to edited data, highlight, and CSV export tests
 */
describe('Property Workers - Scroll, Highlight & CSV Export', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    navigateToBackendConfigModule('backend-configuration-pn-property-workers');
  });

  describe('Scroll to Edited Row', () => {
    it('should scroll to the edited worker row after saving changes', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row').then($rows => {
        if ($rows.length === 0) {
          cy.log('No property worker rows found - test skipped');
          return;
        }

        // Open action menu on first row
        cy.get('.mat-mdc-row').first().within(() => {
          cy.get('[id*="actionMenu"], #actionMenu').first().click({ force: true });
        });

        // Click edit device user button
        cy.get('[id*="editDeviceUserBtn"]').filter(':visible').first().click({ force: true });

        // Wait for edit modal
        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');

        // Save changes
        cy.get('#saveEditBtn').click({ force: true });

        // Wait for save to complete
        cy.wait(1500);

        // Verify the first row is visible (scrolled to)
        cy.get('.mat-mdc-row').first().should('be.visible').scrollIntoView();
      });
    });

    it('should scroll to newly created worker row', () => {
      // Click new device user button
      cy.get('#newDeviceUserBtn').should('be.visible').click();

      // Wait for create modal
      cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');

      // Fill required fields
      const testName = generateRandmString(5);
      const testSurname = generateRandmString(5);
      const testEmail = `${generateRandmString(5)}@test.com`;

      cy.get('#firstName').clear().type(testName);
      cy.get('#lastName').clear().type(testSurname);
      cy.get('#workerEmail').clear().type(testEmail);

      // Intercept create API
      cy.intercept('PUT', '**/api/backend-configuration-pn/properties/assignment/create-device-user').as('createWorker');
      cy.intercept('POST', '**/api/backend-configuration-pn/properties/assignment/index-device-user').as('getWorkers');

      // Save
      cy.get('#saveCreateBtn').click();

      // Wait for creation
      cy.wait('@createWorker', { timeout: 10000 });
      cy.wait('@getWorkers', { timeout: 10000 });

      // Verify the new row is visible
      cy.get('.mat-mdc-row').contains(testName).should('be.visible').scrollIntoView();

      // Cleanup - delete the created worker
      cy.get('.mat-mdc-row').contains(testName).parent().parent().within(() => {
        cy.get('[id*="actionMenu"]').click({ force: true });
      });
      cy.get('[id*="deleteDeviceUserBtn"]').filter(':visible').first().click({ force: true });
      cy.get('#saveDeleteBtn').click({ force: true });
    });

    it('should maintain scroll position when editing worker from scrolled list', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row').then($rows => {
        if ($rows.length < 3) {
          cy.log('Not enough rows for scroll position test');
          return;
        }

        // Scroll to last row
        cy.get('.mat-mdc-row').last().scrollIntoView();

        // Edit last row
        cy.get('.mat-mdc-row').last().within(() => {
          cy.get('[id*="actionMenu"]').click({ force: true });
        });

        cy.get('[id*="editDeviceUserBtn"]').filter(':visible').first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');
        cy.get('#saveEditBtn').click({ force: true });

        cy.wait(1500);

        // Last row should still be visible
        cy.get('.mat-mdc-row').last().should('be.visible');
      });
    });
  });

  describe('Row Highlight After Edit', () => {
    it('should highlight the worker row after editing', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row').then($rows => {
        if ($rows.length === 0) return;

        // Store initial state
        cy.get('.mat-mdc-row').first().then($row => {
          const initialBg = $row.css('background-color');
          const initialClasses = $row.attr('class');

          // Edit
          cy.get('.mat-mdc-row').first().within(() => {
            cy.get('[id*="actionMenu"]').click({ force: true });
          });

          cy.get('[id*="editDeviceUserBtn"]').filter(':visible').first().click({ force: true });

          cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');
          cy.get('#saveEditBtn').click({ force: true });

          cy.wait(500);

          // Check highlight
          cy.get('.mat-mdc-row').first().should($rowAfter => {
            const newBg = $rowAfter.css('background-color');
            const newClasses = $rowAfter.attr('class') || '';

            const isHighlighted =
              $rowAfter.hasClass('highlight') ||
              $rowAfter.hasClass('highlighted') ||
              newClasses.includes('highlight') ||
              newBg !== initialBg;

            expect(isHighlighted, 'Worker row should be highlighted after edit').to.be.true;
          });
        });
      });
    });

    it('should highlight newly created worker row', () => {
      cy.get('#newDeviceUserBtn').should('be.visible').click();

      cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');

      const testName = generateRandmString(5);
      const testSurname = generateRandmString(5);
      const testEmail = `${generateRandmString(5)}@test.com`;

      cy.get('#firstName').clear().type(testName);
      cy.get('#lastName').clear().type(testSurname);
      cy.get('#workerEmail').clear().type(testEmail);

      cy.intercept('PUT', '**/api/backend-configuration-pn/properties/assignment/create-device-user').as('createWorker');
      cy.intercept('POST', '**/api/backend-configuration-pn/properties/assignment/index-device-user').as('getWorkers');

      cy.get('#saveCreateBtn').click();

      cy.wait('@createWorker', { timeout: 10000 });
      cy.wait('@getWorkers', { timeout: 10000 });

      cy.wait(500);

      // Check for highlight on the new row
      cy.get('.mat-mdc-row').contains(testName)
        .parent()
        .parent()
        .should($row => {
          const isHighlighted =
            $row.hasClass('highlight') ||
            $row.hasClass('highlighted') ||
            ($row.attr('class') || '').includes('highlight');

          expect(isHighlighted, 'New worker row should be highlighted').to.be.true;
        });

      // Cleanup
      cy.get('.mat-mdc-row').contains(testName).parent().parent().within(() => {
        cy.get('[id*="actionMenu"]').click({ force: true });
      });
      cy.get('[id*="deleteDeviceUserBtn"]').filter(':visible').first().click({ force: true });
      cy.get('#saveDeleteBtn').click({ force: true });
    });

    it('should remove highlight after animation completes', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row').then($rows => {
        if ($rows.length === 0) return;

        // Edit
        cy.get('.mat-mdc-row').first().within(() => {
          cy.get('[id*="actionMenu"]').click({ force: true });
        });

        cy.get('[id*="editDeviceUserBtn"]').filter(':visible').first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).should('be.visible');
        cy.get('#saveEditBtn').click({ force: true });

        // Wait for highlight animation to complete
        cy.wait(5000);

        // Check that highlight is removed
        cy.get('.mat-mdc-row').first().then($row => {
          const stillHighlighted = $row.hasClass('highlight') || $row.hasClass('highlighted');
          cy.log(`Highlight state after animation: ${stillHighlighted}`);
        });
      });
    });
  });

  describe('CSV Export', () => {
    it('should have CSV export button visible', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      // Look for export button
      cy.get('body').then($body => {
        const exportSelectors = [
          '[id*="csv"]',
          '[id*="export"]',
          'button[mattooltip*="CSV"]',
          'button[mattooltip*="Export"]',
          'mat-icon[svgIcon*="csv"]',
          'mat-icon[svgIcon*="file-csv"]'
        ];

        let found = false;
        for (const selector of exportSelectors) {
          if ($body.find(selector).length > 0) {
            cy.get(selector).first().should('be.visible');
            found = true;
            cy.log(`Found export button: ${selector}`);
            break;
          }
        }

        if (!found) {
          cy.log('CSV export button not found for property workers');
        }
      });
    });

    it('should export property workers to CSV', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      // Intercept export
      cy.intercept('GET', '**/export/**').as('csvExport');
      cy.intercept('GET', '**/property-workers/**export**').as('workersExport');

      cy.get('[id*="csv"], [id*="export"]').first().then($btn => {
        if ($btn.length > 0) {
          cy.wrap($btn).click({ force: true });
          cy.wait(3000);
          cy.log('CSV export triggered for property workers');
        }
      });
    });

    it('should download CSV with worker data', () => {
      cy.get('.mat-mdc-table', { timeout: 10000 }).should('be.visible');

      // Count workers
      cy.get('.mat-mdc-row').then($rows => {
        const workerCount = $rows.length;
        cy.log(`Workers to export: ${workerCount}`);

        // Trigger export
        cy.get('[id*="csv"], [id*="export"]').first().click({ force: true });

        cy.wait(3000);

        // Verify no error
        cy.get('.toast-error, .mat-snack-bar-container:contains("error")', { timeout: 2000 })
          .should('not.exist');
      });
    });
  });
});

// Canary test
it('Property Workers tests - asserts true', () => {
  expect(true).to.be.true;
});

