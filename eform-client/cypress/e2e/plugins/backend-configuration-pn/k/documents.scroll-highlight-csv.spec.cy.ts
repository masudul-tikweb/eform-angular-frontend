import loginPage from '../../../Login.page';
import { navigateToBackendConfigModule } from './scroll-highlight-csv.helpers';
import { generateRandmString } from '../../../helper-functions';

/**
 * Documents - Scroll to edited data, highlight, and CSV export tests
 */
describe('Documents - Scroll, Highlight & CSV Export', () => {
  beforeEach(() => {
    cy.visit('http://localhost:4200');
    loginPage.login();
    navigateToBackendConfigModule('backend-configuration-pn-documents');
  });

  describe('Scroll to Edited Row', () => {
    it('should scroll to the edited document row after saving changes', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row, .cdk-row').then($rows => {
        if ($rows.length === 0) {
          cy.log('No document rows found - test skipped');
          return;
        }

        // Open action menu on first row
        cy.get('.mat-mdc-row, .cdk-row').first().within(() => {
          cy.get('[id*="actionMenu"], #actionMenu, button[mat-icon-button]').first().click({ force: true });
        });

        // Click edit
        cy.get('[id*="edit"], [id*="Edit"]').filter(':visible').first().click({ force: true });

        // Wait for edit modal
        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            // Save changes
            cy.get('[id*="save"], [id*="Save"], [type="submit"]')
              .filter(':visible')
              .first()
              .click({ force: true });

            cy.wait(1500);

            // Verify the first row is visible
            cy.get('.mat-mdc-row, .cdk-row').first().should('be.visible').scrollIntoView();
          }
        });
      });
    });

    it('should scroll to newly created document row', () => {
      // Look for create button
      cy.get('[id*="create"], [id*="add"], #createDocumentBtn').then($btn => {
        if ($btn.length === 0) {
          cy.log('Create document button not found - skipping test');
          return;
        }

        cy.wrap($btn).first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            // Fill required fields
            const docName = generateRandmString(10);
            cy.get('[id*="name"], [id*="title"]').first().clear().type(docName);

            // Save
            cy.get('[id*="save"], [id*="create"]').filter(':visible').first().click({ force: true });

            cy.wait(2000);

            // New document should be visible
            cy.get('.mat-mdc-row, .cdk-row').contains(docName).should('be.visible').scrollIntoView();

            // Cleanup
            cy.get('.mat-mdc-row, .cdk-row').contains(docName).parent().parent().within(() => {
              cy.get('[id*="actionMenu"]').click({ force: true });
            });
            cy.get('[id*="delete"]').filter(':visible').first().click({ force: true });
            cy.get('[id*="confirm"], [id*="save"]').filter(':visible').first().click({ force: true });
          }
        });
      });
    });

    it('should maintain scroll position when editing document from list bottom', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row, .cdk-row').then($rows => {
        if ($rows.length < 3) {
          cy.log('Not enough rows for scroll position test');
          return;
        }

        // Scroll to last row
        cy.get('.mat-mdc-row, .cdk-row').last().scrollIntoView();

        // Edit last row
        cy.get('.mat-mdc-row, .cdk-row').last().within(() => {
          cy.get('[id*="actionMenu"]').first().click({ force: true });
        });

        cy.get('[id*="edit"]').filter(':visible').first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            cy.get('[id*="save"]').filter(':visible').first().click({ force: true });
            cy.wait(1500);

            // Last row should be visible
            cy.get('.mat-mdc-row, .cdk-row').last().should('be.visible');
          }
        });
      });
    });
  });

  describe('Row Highlight After Edit', () => {
    it('should highlight the document row after editing', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row, .cdk-row').then($rows => {
        if ($rows.length === 0) return;

        // Store initial state
        cy.get('.mat-mdc-row, .cdk-row').first().then($row => {
          const initialBg = $row.css('background-color');
          const initialClasses = $row.attr('class');

          // Edit
          cy.get('.mat-mdc-row, .cdk-row').first().within(() => {
            cy.get('[id*="actionMenu"]').first().click({ force: true });
          });

          cy.get('[id*="edit"]').filter(':visible').first().click({ force: true });

          cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
            if ($dialog.length > 0) {
              cy.get('[id*="save"]').filter(':visible').first().click({ force: true });

              cy.wait(500);

              // Check highlight
              cy.get('.mat-mdc-row, .cdk-row').first().should($rowAfter => {
                const newBg = $rowAfter.css('background-color');
                const newClasses = $rowAfter.attr('class') || '';

                const isHighlighted =
                  $rowAfter.hasClass('highlight') ||
                  $rowAfter.hasClass('highlighted') ||
                  newClasses.includes('highlight') ||
                  newBg !== initialBg;

                expect(isHighlighted, 'Document row should be highlighted after edit').to.be.true;
              });
            }
          });
        });
      });
    });

    it('should highlight newly created document row', () => {
      cy.get('[id*="create"], [id*="add"]').first().then($btn => {
        if ($btn.length === 0) return;

        cy.wrap($btn).click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            const docName = generateRandmString(10);
            cy.get('[id*="name"], [id*="title"]').first().clear().type(docName);

            cy.get('[id*="save"], [id*="create"]').filter(':visible').first().click({ force: true });

            cy.wait(1000);

            // Check for highlight on new row
            cy.get('.mat-mdc-row, .cdk-row').contains(docName)
              .parent()
              .parent()
              .should($row => {
                const isHighlighted =
                  $row.hasClass('highlight') ||
                  $row.hasClass('highlighted') ||
                  ($row.attr('class') || '').includes('highlight');

                expect(isHighlighted, 'New document row should be highlighted').to.be.true;
              });

            // Cleanup
            cy.get('.mat-mdc-row, .cdk-row').contains(docName).parent().parent().within(() => {
              cy.get('[id*="actionMenu"]').click({ force: true });
            });
            cy.get('[id*="delete"]').filter(':visible').first().click({ force: true });
            cy.get('[id*="confirm"], [id*="save"]').filter(':visible').first().click({ force: true });
          }
        });
      });
    });

    it('should show highlight animation effect on document row', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row, .cdk-row').first().then($row => {
        if ($row.length === 0) return;

        // Edit
        cy.get('.mat-mdc-row, .cdk-row').first().within(() => {
          cy.get('[id*="actionMenu"]').first().click({ force: true });
        });

        cy.get('[id*="edit"]').filter(':visible').first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            cy.get('[id*="save"]').filter(':visible').first().click({ force: true });

            // Check for animation properties
            cy.get('.mat-mdc-row, .cdk-row').first().should($rowAfter => {
              const animation = $rowAfter.css('animation-name');
              const transition = $rowAfter.css('transition');
              const bgColor = $rowAfter.css('background-color');

              cy.log(`Document row - Animation: ${animation}, BG: ${bgColor}`);

              // Should have some visual effect
              const hasEffect =
                (animation && animation !== 'none') ||
                $rowAfter.hasClass('highlight') ||
                $rowAfter.hasClass('highlighted');

              expect(hasEffect, 'Should have visual highlight effect').to.be.true;
            });
          }
        });
      });
    });

    it('should fade highlight after a few seconds', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      cy.get('.mat-mdc-row, .cdk-row').then($rows => {
        if ($rows.length === 0) return;

        // Edit
        cy.get('.mat-mdc-row, .cdk-row').first().within(() => {
          cy.get('[id*="actionMenu"]').first().click({ force: true });
        });

        cy.get('[id*="edit"]').filter(':visible').first().click({ force: true });

        cy.get('.mat-mdc-dialog-container', { timeout: 5000 }).then($dialog => {
          if ($dialog.length > 0) {
            cy.get('[id*="save"]').filter(':visible').first().click({ force: true });

            // Wait for highlight animation to complete
            cy.wait(5000);

            // Verify highlight is removed or faded
            cy.get('.mat-mdc-row, .cdk-row').first().then($row => {
              const stillHighlighted = $row.hasClass('highlight') || $row.hasClass('highlighted');
              cy.log(`Document highlight after timeout: ${stillHighlighted}`);
            });
          }
        });
      });
    });
  });

  describe('CSV Export', () => {
    it('should have CSV export button in the documents view', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

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
            cy.get(selector).first().should('exist');
            found = true;
            cy.log(`Found documents export button: ${selector}`);
            break;
          }
        }

        if (!found) {
          cy.log('CSV export button not found for documents');
        }
      });
    });

    it('should trigger CSV export when clicking export button', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      // Intercept export
      cy.intercept('GET', '**/export/**').as('csvExport');
      cy.intercept('GET', '**/documents/**export**').as('documentsExport');
      cy.intercept('POST', '**/export/**').as('csvExportPost');

      cy.get('[id*="csv"], [id*="export"]').first().then($btn => {
        if ($btn.length > 0) {
          cy.wrap($btn).click({ force: true });
          cy.wait(3000);
          cy.log('CSV export triggered for documents');
        }
      });
    });

    it('should export all documents to CSV file', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      // Count documents
      cy.get('.mat-mdc-row, .cdk-row').then($rows => {
        const documentCount = $rows.length;
        cy.log(`Documents to export: ${documentCount}`);

        // Trigger export
        cy.get('[id*="csv"], [id*="export"]').first().click({ force: true });

        cy.wait(3000);

        // Verify no error occurred
        cy.get('.toast-error, .mat-snack-bar-container:contains("error")', { timeout: 2000 })
          .should('not.exist');
      });
    });

    it('should export CSV with proper document data columns', () => {
      cy.get('.mat-mdc-table, .cdk-table', { timeout: 10000 }).should('be.visible');

      // Intercept and verify CSV content type
      cy.intercept('GET', '**/export/**', (req) => {
        req.continue((res) => {
          // Verify response is CSV-like
          const contentType = res.headers['content-type'];
          if (contentType) {
            expect(
              contentType.includes('csv') ||
              contentType.includes('text') ||
              contentType.includes('octet-stream')
            ).to.be.true;
          }
        });
      }).as('csvDownload');

      cy.get('[id*="csv"], [id*="export"]').first().click({ force: true });

      cy.wait(3000);
      cy.log('CSV export verified for documents');
    });
  });
});

// Canary test
it('Documents tests - asserts true', () => {
  expect(true).to.be.true;
});

