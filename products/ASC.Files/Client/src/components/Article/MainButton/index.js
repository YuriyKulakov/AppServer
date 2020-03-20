import React from 'react';
import { connect } from 'react-redux';
import PropTypes from 'prop-types';
import { withRouter } from 'react-router';
import {
  MainButton,
  DropDownItem,
  toastr
} from "asc-web-components";
import { withTranslation, I18nextProvider } from 'react-i18next';
import { setAction } from '../../../store/files/actions';
import { isMyDocuments } from '../../../store/files/selectors';
import i18n from '../i18n';
import { utils, constants } from 'asc-web-common';
const { changeLanguage } = utils;
const { FileAction } = constants;

class PureArticleMainButtonContent extends React.Component {

  onCreate = (format) => {
    this.props.setAction(
      {
        type: FileAction.Create,
        extension: format,
        id: -1
      });
  }

  shouldComponentUpdate(nextProps, nextState) {
    return nextProps.isMyDocuments !== this.props.isMyDocuments;
  }

  render() {
    console.log("Files ArticleMainButtonContent render");
    const { t, isMyDocuments } = this.props;

    return (
      <MainButton
        isDisabled={!isMyDocuments}
        isDropdown={true}
        text={t('Actions')}
      >
        <DropDownItem
          icon="ActionsDocumentsIcon"
          label={t('NewDocument')}
          onClick={this.onCreate.bind(this, 'docx')}
        />
        <DropDownItem
          icon="SpreadsheetIcon"
          label={t('NewSpreadsheet')}
          onClick={this.onCreate.bind(this, 'xlsx')}
        />
        <DropDownItem
          icon="ActionsPresentationIcon"
          label={t('NewPresentation')}
          onClick={this.onCreate.bind(this, 'pptx')}
        />
        <DropDownItem
          icon="CatalogFolderIcon"
          label={t('NewFolder')}
          onClick={this.onCreate}
        />
        <DropDownItem isSeparator />
        <DropDownItem
          icon="ActionsUploadIcon"
          label={t('Upload')}
          onClick={() => toastr.info("Upload click")}
          disabled
        />
      </MainButton>
    );
  };
};

const ArticleMainButtonContentContainer = withTranslation()(PureArticleMainButtonContent);

const ArticleMainButtonContent = (props) => {
  changeLanguage(i18n);
  return (<I18nextProvider i18n={i18n}><ArticleMainButtonContentContainer {...props} /></I18nextProvider>);
};

ArticleMainButtonContent.propTypes = {
  isAdmin: PropTypes.bool.isRequired,
  history: PropTypes.object.isRequired
};

const mapStateToProps = (state) => {
  return {
    settings: state.auth.settings,
    isMyDocuments: isMyDocuments(state.files.rootFolders.my.id, state.files.selectedFolder.id)
  }
}

export default connect(mapStateToProps, { setAction })(withRouter(ArticleMainButtonContent));