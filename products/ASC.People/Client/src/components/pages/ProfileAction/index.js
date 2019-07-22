import React from "react";
import { connect } from "react-redux";
import PropTypes from "prop-types";
import { withRouter } from "react-router";
import _ from "lodash";
import { PageLayout } from "asc-web-components";
import {ArticleHeaderContent, ArticleBodyContent} from '../../Article';
import {SectionHeaderContent, SectionBodyContent} from './Section';

const ProfileAction = (props) => {
  return (
      <PageLayout
          articleHeaderContent={<ArticleHeaderContent />}
          articleBodyContent={<ArticleBodyContent />}
          sectionHeaderContent={<SectionHeaderContent {...props} />}
          sectionBodyContent={<SectionBodyContent {...props} /> }
      />
  );
};

ProfileAction.propTypes = {
  profile: PropTypes.object.isRequired,
  history: PropTypes.object.isRequired,
  isLoaded: PropTypes.bool
};

function mapStateToProps(state) {
  return {
    profile: {},
    isLoaded: state.auth.isLoaded
  };
}

export default connect(mapStateToProps)(withRouter(ProfileAction));