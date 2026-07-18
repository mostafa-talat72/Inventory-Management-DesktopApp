import { Bill, Order, Session, ItemPayment, SessionPayment } from '../services/api';
import { aggregateItemsWithPayments, AggregatedItem } from './billAggregation';
import { formatDecimal, formatCurrency as formatCurrencyUtil, getCurrencySymbol, getDisplayNumber } from './formatters';
import QRCode from 'qrcode';
import { api } from '../services/api';
import { getLocaleFromLanguage } from './localeMapper';
import type { TFunction } from 'i18next';

// Function to determine the appropriate link for QR Code based on priority
const getSocialLinkForQR = (socialLinks: any): { link: string; platform: string } | null => {
  if (!socialLinks) return null;
  
  // Priority order with platform names
  const priorityOrder = [
    { key: 'facebook', name: 'Facebook' },
    { key: 'instagram', name: 'Instagram' }, 
    { key: 'location', name: 'Google Maps' },
    { key: 'whatsapp', name: 'WhatsApp' },
    { key: 'telegram', name: 'Telegram' },
    { key: 'twitter', name: 'Twitter' },
    { key: 'linkedin', name: 'LinkedIn' },
    { key: 'youtube', name: 'YouTube' },
    { key: 'tiktok', name: 'TikTok' }
  ];
  
  for (const platform of priorityOrder) {
    if (socialLinks[platform.key] && socialLinks[platform.key].trim() !== '') {
      let link = socialLinks[platform.key].trim();
      
      // Format WhatsApp link if it's a phone number
      if (platform.key === 'whatsapp' && !link.startsWith('http')) {
        // Remove unwanted characters from phone number
        const phoneNumber = link.replace(/[^\d+]/g, '');
        link = `https://wa.me/${phoneNumber}`;
      }
      
      // Ensure link starts with http or https
      if (!link.startsWith('http://') && !link.startsWith('https://')) {
        // Add https:// for links without protocol
        if (platform.key === 'location' && link.includes('maps.google')) {
          link = link.startsWith('//') ? `https:${link}` : `https://${link}`;
        } else if (platform.key !== 'whatsapp') {
          link = `https://${link}`;
        }
      }
      
      return { link, platform: platform.name };
    }
  }
  
  return null;
};

// Function to generate QR Code
const generateQRCode = async (text: string): Promise<string> => {
  try {
    const qrCodeDataURL = await QRCode.toDataURL(text, {
      width: 150,
      margin: 2,
      color: {
        dark: '#000000',
        light: '#FFFFFF'
      },
      errorCorrectionLevel: 'M',
      type: 'image/png'
    });
    return qrCodeDataURL;
  } catch (error) {
    console.error('Error generating QR code:', error);
    return '';
  }
};

export const printBill = async (
  bill: Bill, 
  fallbackOrganizationName?: string,
  language: string = 'ar',
  t: TFunction = ((key: string) => key) as TFunction
) => {
  // Get establishment name from bill data or use fallback
  let organizationName = fallbackOrganizationName || t('billPrint.defaultEstablishment') || 'Cafe Management System';
  let organizationData: any = null;
  let qrCodeDataURL = '';
  
  // If organization exists in bill data
  if (bill.organization) {
    if (typeof bill.organization === 'object' && bill.organization.name && (bill.organization as any).socialLinks) {
      // If organization is a fully populated object
      organizationName = bill.organization.name;
      organizationData = bill.organization;
    
    } else if (typeof bill.organization === 'object' && (bill.organization._id || bill.organization.name)) {
      // If organization is an object but not fully loaded
      const orgId = bill.organization._id;
      organizationName = bill.organization.name || organizationName;
      try {
        const orgResponse = await api.getOrganizationById(orgId);
        if (orgResponse.success && orgResponse.data) {
          organizationName = orgResponse.data.name || organizationName;
          organizationData = orgResponse.data;
        } else {
          // If fetching specific organization fails, try fetching current user's organization
          const fallbackOrgResponse = await api.getOrganization();
          if (fallbackOrgResponse.success && fallbackOrgResponse.data) {
            organizationName = fallbackOrgResponse.data.name || organizationName;
            organizationData = fallbackOrgResponse.data;
          }
        }
      } catch (error) {
        console.warn('Failed to fetch organization data for object ID:', error);
        // Last attempt to fetch current organization data
        try {
          const fallbackOrgResponse = await api.getOrganization();
          if (fallbackOrgResponse.success && fallbackOrgResponse.data) {
            organizationName = fallbackOrgResponse.data.name || organizationName;
            organizationData = fallbackOrgResponse.data;
          }
        } catch (fallbackError) {
          console.warn('All organization fetch attempts failed:', fallbackError);
        }
      }
    } else if (typeof bill.organization === 'string') {
      // If organization is just a string ID, try to fetch data using the specific ID
      try {
        // Use dedicated endpoint to get specific organization data
        const orgResponse = await api.getOrganizationById(bill.organization);
        if (orgResponse.success && orgResponse.data) {
          organizationName = orgResponse.data.name || organizationName;
          organizationData = orgResponse.data;
        } else {
          // If fetching specific organization fails, try fetching current user's organization
          const fallbackOrgResponse = await api.getOrganization();
          if (fallbackOrgResponse.success && fallbackOrgResponse.data) {
            organizationName = fallbackOrgResponse.data.name || organizationName;
            organizationData = fallbackOrgResponse.data;
          }
        }
      } catch (error) {
        console.warn('Failed to fetch organization data:', error);
        // Last attempt to fetch current organization data
        try {
          const fallbackOrgResponse = await api.getOrganization();
          if (fallbackOrgResponse.success && fallbackOrgResponse.data) {
            organizationName = fallbackOrgResponse.data.name || organizationName;
            organizationData = fallbackOrgResponse.data;
          }
        } catch (fallbackError) {
          console.warn('All organization fetch attempts failed:', fallbackError);
        }
      }
    }
  } else {
    // Try to fetch organization data from current user
    try {
      const orgResponse = await api.getOrganization();
      if (orgResponse.success && orgResponse.data) {
        organizationName = orgResponse.data.name || organizationName;
        organizationData = orgResponse.data;
      }
    } catch (error) {
      console.warn('Failed to fetch current organization data:', error);
    }
  }
  
  // Ensure printSettings is available
  let showQRCode = true;
  if (organizationData?.printSettings?.printQRCode !== undefined) {
    showQRCode = organizationData.printSettings.printQRCode;
  } else if (bill.organization) {
    try {
      const orgId = typeof bill.organization === 'object' ? (bill.organization as any)._id || bill.organization : bill.organization;
      const orgRes = await api.getOrganizationById(orgId);
      if (orgRes.success && orgRes.data) {
        organizationData = orgRes.data;
        showQRCode = orgRes.data.printSettings?.printQRCode !== false;
      }
    } catch (_) {}
  }
  
  // Generate QR Code if organization data is available and printing QR is enabled
  let qrInfo: { link: string; platform: string } | null = null;
  if (showQRCode && organizationData && organizationData.socialLinks) {
    qrInfo = getSocialLinkForQR(organizationData.socialLinks);
    if (qrInfo) {
      qrCodeDataURL = await generateQRCode(qrInfo.link);
    }
  }
  
  // Generate fallback QR Code if no social links found
  if (showQRCode && !qrCodeDataURL && organizationData) {
    let fallbackText = organizationName;
    
    // Add additional information if available
    if (organizationData.phone) {
      fallbackText += `\n${t('billPrint.phone')}: ${organizationData.phone}`;
    }
    if (organizationData.address) {
      fallbackText += `\n${t('billPrint.address')}: ${organizationData.address}`;
    }
    if (organizationData.email) {
      fallbackText += `\n${t('billPrint.email')}: ${organizationData.email}`;
    }
    
    qrCodeDataURL = await generateQRCode(fallbackText);
    qrInfo = { link: fallbackText, platform: t('billPrint.organizationInfo') };
  }
  
  // Generate basic QR Code if no data found
  if (showQRCode && !qrCodeDataURL && organizationName && organizationName !== t('billPrint.defaultEstablishment')) {
    qrCodeDataURL = await generateQRCode(organizationName);
    qrInfo = { link: organizationName, platform: t('billPrint.organizationName') };
  }
  
  const dir = language === 'ar' ? 'rtl' : 'ltr';
  const locale = getLocaleFromLanguage(language);
  
  // Format date - uses organization timezone
  const formatDate = (dateString: string | Date) => {
    const date = new Date(dateString);
    const organizationTimezone = localStorage.getItem('organizationTimezone') || 'Africa/Cairo';
    return date.toLocaleString(locale, {
      timeZone: organizationTimezone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hour12: true
    });
  };

  // Format number without currency
  const formatNumber = (amount: number | undefined | null) => {
    const safeAmount = amount ?? 0;
    return formatDecimal(Math.round(safeAmount), language);
  };

  // Format quantity
  const formatQuantity = (qty: number) => {
    return formatDecimal(qty, language);
  };

  // Generate order items table
  const generateOrderItemsTable = (orders: Order[], itemPayments?: ItemPayment[], billStatus?: string, billPaid?: number, billTotal?: number) => {
    const aggregatedItems = aggregateItemsWithPayments(
      orders,
      itemPayments,
      billStatus,
      billPaid,
      billTotal
    );
    
    if (aggregatedItems.length === 0) {
      return '';
    }
    
    const itemsRows = aggregatedItems.map((item: AggregatedItem) => {
      const addonsText = item.addons && item.addons.length > 0
        ? ` (${item.addons.map(a => a.name).join(', ')})`
        : '';
      
      const isPaidFully = item.paidQuantity >= item.totalQuantity;
      const statusIcon = isPaidFully ? '✓' : item.paidQuantity > 0 ? '◐' : '○';
      
      return `
        <tr>
          <td class="item-name">${statusIcon} ${item.name}${addonsText}</td>
          <td class="item-quantity">${formatQuantity(item.totalQuantity)}</td>
          <td class="item-paid-qty">${formatQuantity(item.paidQuantity)}</td>
          <td class="item-total">${formatNumber(item.price * item.totalQuantity)}</td>
        </tr>
      `;
    }).join('');
    
    return `
      <div class="section-title">${t('billPrint.orders')}</div>
      <table class="items-table">
        <thead>
          <tr>
            <th class="col-name" style="width: 50%;">${t('billPrint.item')}</th>
            <th class="col-quantity" style="width: 16.67%;">${t('billPrint.quantity')}</th>
            <th class="col-paid-qty" style="width: 16.67%;">${t('billPrint.paid')}</th>
            <th class="col-total" style="width: 16.66%;">${t('billPrint.total')}</th>
          </tr>
        </thead>
        <tbody>
          ${itemsRows}
        </tbody>
      </table>
    `;
  };

  // Generate sessions table
  const generateSessionsTable = (sessions: Session[], sessionPayments?: SessionPayment[]) => {
    if (!sessions || sessions.length === 0) return '';

    const sessionsRows = sessions.map(session => {
      const startTime = new Date(session.startTime);
      const endTime = session.endTime ? new Date(session.endTime) : new Date();
      const durationInMinutes = Math.floor((endTime.getTime() - startTime.getTime()) / (1000 * 60));
      const hours = Math.floor(durationInMinutes / 60);
      const minutes = durationInMinutes % 60;
      
      // Format duration based on language
      let durationText = '';
      if (language === 'ar') {
        if (hours > 0 && minutes > 0) {
          durationText = `${formatQuantity(hours)} س ${formatQuantity(minutes)} د`;
        } else if (hours > 0) {
          durationText = `${formatQuantity(hours)} س`;
        } else {
          durationText = `${formatQuantity(minutes)} د`;
        }
      } else if (language === 'fr') {
        if (hours > 0 && minutes > 0) {
          durationText = `${formatQuantity(hours)}h ${formatQuantity(minutes)}m`;
        } else if (hours > 0) {
          durationText = `${formatQuantity(hours)}h`;
        } else {
          durationText = `${formatQuantity(minutes)}m`;
        }
      } else {
        if (hours > 0 && minutes > 0) {
          durationText = `${formatQuantity(hours)}h ${formatQuantity(minutes)}m`;
        } else if (hours > 0) {
          durationText = `${formatQuantity(hours)}h`;
        } else {
          durationText = `${formatQuantity(minutes)}m`;
        }
      }
      
      const finalCost = session.finalCost ?? session.totalCost ?? 0;
      
      // Find session payment details
      const sessionPayment = sessionPayments?.find(sp => 
        sp.sessionId === session._id || sp.sessionId === session.id
      );
      const paidAmount = sessionPayment?.paidAmount || 0;
      
      // Calculate remaining amount correctly
      let remainingAmount: number;
      if (sessionPayment && sessionPayment.remainingAmount !== undefined) {
        remainingAmount = sessionPayment.remainingAmount;
      } else {
        // If no session payment record, calculate manually
        remainingAmount = Math.max(0, finalCost - paidAmount);
      }
      
      // Status icon based on payment
      const isPaidFully = remainingAmount === 0 && finalCost > 0;
      const statusIcon = isPaidFully ? '✓' : paidAmount > 0 ? '◐' : '○';
      
      return `
        <tr>
          <td class="item-name">${statusIcon} ${session.deviceName || session.deviceNumber || t('billPrint.unspecified')}</td>
          <td class="item-quantity">${durationText}</td>
          <td class="item-paid">${formatNumber(paidAmount)}</td>
          <td class="item-total">${formatNumber(finalCost)}</td>
        </tr>
      `;
    }).join('');
    
    return `
      <div class="section-title">${t('billPrint.sessions')}</div>
      <table class="items-table sessions-table">
        <thead>
          <tr>
            <th class="col-name" style="width: 40%;">${t('billPrint.device')}</th>
            <th class="col-quantity" style="width: 20%;">${t('billPrint.duration')}</th>
            <th class="col-paid" style="width: 20%;">${t('billPrint.paid')}</th>
            <th class="col-total" style="width: 20%;">${t('billPrint.total')}</th>
          </tr>
        </thead>
        <tbody>
          ${sessionsRows}
        </tbody>
      </table>
    `;
  };

  // Get currency from localStorage
  const organizationCurrency = localStorage.getItem('organizationCurrency') || 'EGP';
  
  // Get currency symbol based on language using the imported function
  const currencySymbol = getCurrencySymbol(organizationCurrency, language);

  // Main receipt HTML
  const receiptHTML = `
    <!DOCTYPE html>
    <html dir="${dir}" lang="${language}">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>${getDisplayNumber(bill.billNumber) || ''}</title>
      <style>
        @import url('https://fonts.googleapis.com/css2?family=Tajawal:wght@400;500;700;800;900&display=swap');
        * { 
          font-family: 'Tajawal', sans-serif; 
          -webkit-print-color-adjust: exact;
          print-color-adjust: exact;
          box-sizing: border-box;
        }
        body { 
          margin: 0; 
          padding: 8px 8px; 
          font-size: 11px; 
          color: #000; 
          font-weight: 600;
          width: auto;
          max-width: auto;
          text-align: center;
          direction: ${dir};
        }
        .header { 
          text-align: center; 
          margin-bottom: 8px;
          margin-top: 0;
          font-weight: 700;
          border-bottom: 2px dashed #000;
          padding-bottom: 6px;
        }
        .org-name { 
          font-size: 1.4em; 
          font-weight: 900; 
          margin-bottom: 6px; 
          color: #000;
        }
        .title { 
          font-size: 1.1em; 
          font-weight: 800; 
          margin-bottom: 6px; 
          color: #000;
        }
        .info { 
          margin-bottom: 4px; 
          font-weight: 600;
          font-size: 0.9em;
        }
        .divider { 
          border-top: 2px dashed #000; 
          margin: 10px 0; 
        }
        .section-title {
          font-size: 1.1em;
          font-weight: 800;
          margin: 10px 0 6px 0;
          text-align: center;
          background: #e0e0e0;
          padding: 4px;
          border-radius: 4px;
        }
        .items-table { 
          width: 100%;
          border-collapse: collapse;
          margin-bottom: 10px;
          font-size: 0.85em;
          border: 2px solid #000;
          table-layout: fixed;
        }
        .items-table thead {
          background: #e0e0e0;
          font-weight: 800;
        }
        .items-table th {
          padding: 3px 3px;
          text-align: center;
          border: 1.5px solid #000;
          font-size: 0.9em;
          word-wrap: break-word;
        }
        .items-table td {
          padding: 3px 3px;
          text-align: center;
          border: 1px solid #000;
          font-weight: 600;
          word-wrap: break-word;
          overflow-wrap: break-word;
        }
        .items-table .item-name {
          text-align: center;
          font-weight: 700;
          padding-${dir === 'rtl' ? 'right' : 'left'}: 5px;
          width: 50% !important;
        }
        .items-table .item-quantity {
          width: 16.67% !important;
        }
        .items-table .item-paid-qty {
          width: 16.67% !important;
          color: #4caf50;
          font-weight: 700;
        }
        .items-table .item-total {
          width: 16.66% !important;
        }
        .items-table.sessions-table .item-quantity {
          width: 20% !important;
        }
        .items-table.sessions-table .item-paid {
          width: 20% !important;
          color: #4caf50;
          font-weight: 700;
        }
        .items-table.sessions-table .item-total {
          width: 20% !important;
        }
        .items-table th:first-child {
          text-align: center;
          padding-${dir === 'rtl' ? 'right' : 'left'}: 5px;
        }
        .total-section { 
          margin-top: 12px;
          text-align: center;
          font-weight: 800;
        }
        .total-row {
          text-align: center;
          padding: 6px 8px;
          margin-bottom: 4px;
          font-size: 1.1em;
          font-weight: 700;
        }
        .total-row.grand-total {
          font-size: 1.4em;
          font-weight: 900;
          background: #000;
          color: #fff;
          border-radius: 4px;
          margin-top: 8px;
          margin-bottom: 8px;
          padding: 8px;
        }
        .total-row.paid {
          font-size: 1.3em;
          font-weight: 900;
          color: #4caf50;
        }
        .total-row.remaining {
          font-size: 1.3em;
          font-weight: 900;
          color: #ff9800;
        }
        .footer { 
          margin-top: 12px; 
          text-align: center; 
          font-size: 1.1em; 
          color: #000;
          border-top: 2px dashed #000;
          padding-top: 10px;
          padding-bottom: 10px;
          font-weight: 800;
        }
        .qr-section {
          margin: 10px 0;
          text-align: center;
          page-break-inside: avoid;
          border: 1px dashed #ccc;
          padding: 8px;
          border-radius: 8px;
        }
        .qr-code {
          margin: 8px auto;
          display: block;
          border: 2px solid #000;
          border-radius: 8px;
          padding: 4px;
          background: #fff;
          max-width: 120px;
          height: auto;
        }
        .qr-text {
          font-size: 0.9em;
          color: #333;
          margin-top: 5px;
          font-weight: 700;
          line-height: 1.2;
        }
        .qr-subtitle {
          font-size: 0.8em;
          color: #666;
          margin-top: 2px;
          font-weight: 600;
        }
        
        @media print {
          .qr-section {
            page-break-inside: avoid;
            border: 1px dashed #000 !important;
          }
          .qr-code {
            border: 2px solid #000 !important;
            background: #fff !important;
          }
          .qr-text {
            color: #000 !important;
          }
          .qr-subtitle {
            color: #333 !important;
          }
        }
        .thank-you { 
          text-align: center; 
          margin-top: 10px; 
          margin-bottom: 8px;
          font-size: 1.1em; 
          font-weight: 700; 
        }
        strong { 
          font-weight: 800; 
        }
        
        @media print {
          @page { 
            size:auto; 
            margin: 0; 
          }
          body { 
            margin: 0; 
            padding: 0; 
            font-weight: 600;
            width: auto;
          }
          .no-print { display: none !important; }
          .items-table {
            border: 2px solid #000 !important;
          }
          .items-table th,
          .items-table td {
            border: 1px solid #000 !important;
          }
          * {
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
          }
        }
        

        
        @media screen {
          body {
            max-width: auto;
            margin: 0 auto;
            background: #fff;
          }
        }
      </style>
    </head>
    <body>
      <div class="header">
        ${organizationName ? `<div class="org-name">${organizationName}</div>` : `<div class="org-name">${t('billPrint.defaultEstablishment')}</div>`}
        <div class="title" style="font-weight: 900; font-size: 22px;">${getDisplayNumber(bill.billNumber) || ''}</div>
        <div class="info">${formatDate(bill.createdAt || new Date())}</div>
        ${bill.table?.number ? `<div class="info" style="font-weight: 900; font-size: 1.2em; color: #000; margin: 8px 0;"><span style="background: #000; color: #fff; padding: 2px 8px; border-radius: 3px;">${t('billPrint.table')}</span> <strong style="font-size: 1.4em;">${bill.table.number}</strong></div>` : (bill.customerName ? `<div class="info">${t('billPrint.customer')}: ${bill.customerName}</div>` : '')}
        ${bill.customerPhone ? `<div class="info">${t('billPrint.phone')}: ${bill.customerPhone}</div>` : ''}
      </div>

      ${bill.orders && bill.orders.length > 0 ? generateOrderItemsTable(bill.orders, bill.itemPayments, bill.status, bill.paid, bill.total) : ''}

      ${bill.sessions && bill.sessions.length > 0 ? generateSessionsTable(bill.sessions, bill.sessionPayments) : ''}

      <div class="divider"></div>

      <div class="total-section">
        ${bill.discount && bill.discount > 0 ? `
          <div class="total-row">
            ${t('billPrint.discount')}: ${formatNumber(bill.discount)}
          </div>
        ` : ''}
        ${bill.tax && bill.tax > 0 ? `
          <div class="total-row">
            ${t('billPrint.tax')}: ${formatNumber(bill.tax)}
          </div>
        ` : ''}
        <div class="total-row grand-total">
          ${t('billPrint.total')}: ${formatNumber(bill.total || 0)} ${currencySymbol}
        </div>
        <div class="total-row paid">
          ${t('billPrint.paid')}: ${formatNumber(bill.paid || 0)} ${currencySymbol}
        </div>
        <div class="total-row remaining">
          ${t('billPrint.remaining')}: ${formatNumber(bill.remaining || 0)} ${currencySymbol}
        </div>
      </div>

      <div class="thank-you">${t('billPrint.thankYou')}</div>
      
      ${qrCodeDataURL && qrInfo ? `
        <div class="qr-section">
          <img src="${qrCodeDataURL}" alt="${t('billPrint.qrCode')}" class="qr-code" />
          <div class="qr-text">${t('billPrint.contactVia')} ${organizationName}</div>
          <div class="qr-subtitle">${t('billPrint.via')} ${qrInfo.platform}</div>
        </div>
      ` : ''}
      
      <div class="footer">
        <strong style="font-weight: 900; font-size: 14px;">${t('billPrint.footer')}</strong>
      </div>

      <div class="no-print" style="margin-top: 20px; text-align: center; padding: 10px;">
        <button onclick="window.print()" style="
          background: #4CAF50;
          color: white;
          border: none;
          padding: 10px 20px;
          text-align: center;
          text-decoration: none;
          display: inline-block;
          font-size: 14px;
          font-weight: 700;
          cursor: pointer;
          border-radius: 4px;
        ">
          ${t('billPrint.printButton')}
        </button>
      </div>
    </body>
    </html>
  `;

  // Create a hidden iframe for printing
  const printFrame = document.createElement('iframe');
  printFrame.style.position = 'absolute';
  printFrame.style.top = '-1000px';
  printFrame.style.left = '-1000px';
  printFrame.style.width = '0';
  printFrame.style.height = '0';
  printFrame.style.border = 'none';
  
  document.body.appendChild(printFrame);
  
  const frameDoc = printFrame.contentDocument || printFrame.contentWindow?.document;
  if (frameDoc) {
    frameDoc.open();
    frameDoc.write(receiptHTML);
    frameDoc.close();
    
    // Wait for content to load then print
    setTimeout(() => {
      try {
        printFrame.contentWindow?.focus();
        printFrame.contentWindow?.print();
        
        // Clean up after printing
        setTimeout(() => {
          document.body.removeChild(printFrame);
        }, 100);
      } catch (error) {
        console.error('Print error:', error);
        // Fallback to opening in new window if iframe printing fails
        const printWindow = window.open('', '_blank');
        if (printWindow) {
          printWindow.document.open();
          printWindow.document.write(receiptHTML);
          printWindow.document.close();
          printWindow.onload = () => {
            setTimeout(() => {
              printWindow.print();
              setTimeout(() => {
                if (!printWindow.closed) {
                  printWindow.close();
                }
              }, 100);
            }, 50);
          };
        }
        // Clean up iframe
        document.body.removeChild(printFrame);
      }
    }, 50);
  } else {
    // Fallback to original method if iframe fails
    document.body.removeChild(printFrame);
    const printWindow = window.open('', '_blank');
    if (printWindow) {
      printWindow.document.open();
      printWindow.document.write(receiptHTML);
      printWindow.document.close();
      printWindow.onload = () => {
        setTimeout(() => {
          printWindow.print();
          setTimeout(() => {
            if (!printWindow.closed) {
              printWindow.close();
            }
          }, 100);
        }, 50);
      };
    } else {
      const alertMsg = language === 'ar' 
        ? 'الرجاء السماح بالنوافذ المنبثقة لطباعة الفاتورة'
        : language === 'fr'
        ? 'Veuillez autoriser les fenêtres contextuelles pour imprimer la facture'
        : 'Please allow pop-ups to print the bill';
      alert(alertMsg);
    }
  }
};

export default printBill;
