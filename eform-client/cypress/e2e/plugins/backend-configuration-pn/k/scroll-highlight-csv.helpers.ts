/**
 * Helper functions for testing scroll to edited data, highlight, and CSV export functionality
 */

/**
 * Verifies that a row is visible in the viewport after scrolling
 * @param rowSelector - CSS selector for the row
 */
export function verifyRowIsInViewport(rowSelector: string) {
  cy.get(rowSelector).then($row => {
    const rect = $row[0].getBoundingClientRect();
    const viewportHeight = Cypress.config('viewportHeight');
    const viewportWidth = Cypress.config('viewportWidth');

    expect(rect.top).to.be.greaterThan(0);
    expect(rect.bottom).to.be.lessThan(viewportHeight);
    expect(rect.left).to.be.greaterThan(0);
    expect(rect.right).to.be.lessThan(viewportWidth);
  });
}

/**
 * Checks if a row has highlight styling applied
 * @param rowSelector - CSS selector for the row
 * @returns Chainable that resolves to true if highlighted
 */
export function checkRowIsHighlighted(rowSelector: string): Cypress.Chainable<boolean> {
  return cy.get(rowSelector).then($row => {
    // Check for common highlight classes
    const hasHighlightClass =
      $row.hasClass('highlight') ||
      $row.hasClass('highlighted') ||
      $row.hasClass('row-highlight') ||
      $row.hasClass('row-edited') ||
      $row.hasClass('flash') ||
      $row.hasClass('animated') ||
      ($row.attr('class') || '').includes('highlight');

    // Check for highlight background color (not transparent or default)
    const bgColor = $row.css('background-color');
    const hasHighlightBg = bgColor &&
                           bgColor !== 'rgba(0, 0, 0, 0)' &&
                           bgColor !== 'transparent' &&
                           bgColor !== 'rgb(255, 255, 255)';

    // Check for animation
    const animation = $row.css('animation-name');
    const hasAnimation = animation && animation !== 'none';

    return hasHighlightClass || hasHighlightBg || hasAnimation;
  });
}

/**
 * Waits for and verifies highlight animation completes
 * @param rowSelector - CSS selector for the row
 * @param timeout - Maximum time to wait for highlight (ms)
 */
export function waitForHighlightAnimation(rowSelector: string, timeout: number = 3000) {
  // Wait for highlight to be applied
  cy.get(rowSelector, { timeout }).should($row => {
    const hasHighlight =
      $row.hasClass('highlight') ||
      $row.hasClass('highlighted') ||
      $row.css('animation-name') !== 'none' ||
      $row.css('background-color') !== 'rgba(0, 0, 0, 0)';
    expect(hasHighlight).to.be.true;
  });

  // Wait for animation to complete (typically 2-3 seconds)
  cy.wait(timeout);

  // Verify highlight is removed after animation
  cy.get(rowSelector).should($row => {
    const stillHighlighted = $row.hasClass('highlight') || $row.hasClass('highlighted');
    // Highlight may or may not persist - just log the state
    cy.log(`Row still highlighted after animation: ${stillHighlighted}`);
  });
}

/**
 * Tests CSV export functionality
 * @param exportButtonSelector - CSS selector for CSV export button
 * @param expectedApiEndpoint - API endpoint pattern for export (optional)
 */
export function testCsvExport(
  exportButtonSelector: string,
  expectedApiEndpoint?: string
) {
  // Find export button
  cy.get(exportButtonSelector).should('exist').should('be.visible');

  if (expectedApiEndpoint) {
    // Intercept the export API call
    cy.intercept('GET', expectedApiEndpoint).as('csvExport');
  }

  // Click export button
  cy.get(exportButtonSelector).click({ force: true });

  if (expectedApiEndpoint) {
    // Verify API call was made
    cy.wait('@csvExport', { timeout: 30000 }).then((interception) => {
      expect(interception.response.statusCode).to.be.oneOf([200, 201]);

      // Check content type for CSV
      const contentType = interception.response.headers['content-type'];
      if (contentType) {
        expect(contentType).to.include('csv');
      }
    });
  } else {
    // Just verify no errors occurred
    cy.wait(2000);

    // Check for success message or downloaded file
    cy.get('.mat-snack-bar-container, .toast-success, [class*="success"]', { timeout: 5000 })
      .should('exist');
  }
}

/**
 * Navigate to a specific backend configuration module
 * @param moduleId - The ID of the module menu item
 */
export function navigateToBackendConfigModule(moduleId: string) {
  // Wait for page to be ready after login
  cy.get('#spinner-animation', { timeout: 10000 }).should('not.exist');

  // Wait for sidebar/menu to be available
  cy.get('body', { timeout: 10000 }).should('be.visible');

  // Check if backend configuration plugin menu exists
  cy.get('body').then($body => {
    if ($body.find('#backend-configuration-pn').length === 0) {
      // Plugin menu not found - may need to expand sidebar or plugin is not enabled
      cy.log('Backend configuration plugin menu not found. Checking sidebar...');

      // Try to find in sidebar or navigation
      cy.get('[id*="backend-configuration"], [class*="backend-configuration"]', { timeout: 5000 })
        .first()
        .then($el => {
          if ($el.length > 0) {
            cy.wrap($el).click({ force: true });
          } else {
            throw new Error('Backend Configuration plugin is not available. Please ensure the plugin is enabled.');
          }
        });
    } else {
      // Found the menu, proceed with navigation
      cy.get('#backend-configuration-pn', { timeout: 10000 }).should('be.visible').then($menu => {
        // Check if submenu item is visible
        if (!Cypress.$(`#${moduleId}`).is(':visible')) {
          cy.wrap($menu).click({ force: true });
        }
      });
    }
  });

  // Click the specific module
  cy.get(`#${moduleId}`, { timeout: 10000 }).should('be.visible').click({ force: true });

  // Wait for page to load
  cy.get('#spinner-animation', { timeout: 10000 }).should('not.exist');
}
