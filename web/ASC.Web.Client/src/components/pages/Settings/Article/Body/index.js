import React from 'react';
import { utils } from 'asc-web-components';
import { connect } from 'react-redux';
import {
  TreeMenu,
  TreeNode,
  Icons
} from "asc-web-components";
import { selectSettingNode } from '../../../../../store/auth/actions';

const getItems = data => {
  return data.map(item => {
    if (item.children && item.children.length) {
      return (
        <TreeNode
          title={item.title}
          key={item.key}
          icon={item.icon && React.createElement(Icons[item.icon], {
            size: 'scale',
            isfill: true,
            color: 'dimgray',
          })}
        >
          {getItems(item.children)}
        </TreeNode>
      );
    }
    return (
      <TreeNode
        key={item.key}
        title={item.title}
        icon={item.icon && React.createElement(Icons[item.icon], {
          size: 'scale',
          isfill: true,
          color: 'dimgray',
        })}
      />
    );
  });
};

class ArticleBodyContent extends React.Component {

  constructor() {
    super();
    this.state = {
      selectedKeys: ['0-0']
    }
  }

  shouldComponentUpdate(nextProps) {
    if (!utils.array.isArrayEqual(nextProps.selectedKeys, this.props.selectedKeys)) {
      return true;
    }

    if (!utils.array.isArrayEqual(nextProps.data, this.props.data)) {
      return true;
    }

    return false;
  }

  getSelectedTitle = key => {
    const { data } = this.props;
    const length = key.length;
    if (length === 1) {
      return data[key].title;
    }
    else if (length === 3) {
      return data[key[0]].children[key[2]].title;
    }
  }
  onSelect = value => {
    const { data, selectedKeys, selectSettingNode } = this.props;

    if (value) {
      if (utils.array.isArrayEqual(value, selectedKeys)) {

        return;
      }
      if (value[0].length === 3) {
        const selectedTitle = this.getSelectedTitle(value[0]);
        selectSettingNode(value, selectedTitle);
      }
      else if (value[0].length === 1 && (value[0].toString() !== selectedKeys.toString()[0] || selectedKeys.toString()[2] !== '0')) {
        const selectedKeys = data[value].children ? [`${value.toString()}-0`] : value;
        const selectedTitle = this.getSelectedTitle(selectedKeys[0]);
        selectSettingNode(selectedKeys, selectedTitle);
      }
    }
  };

  switcherIcon = obj => {
    if (obj.isLeaf) {
      return null;
    }
    if (obj.expanded) {
      return (
        <Icons.ExpanderDownIcon size="scale" isfill={true} color="dimgray" />
      );
    } else {
      return (
        <Icons.ExpanderRightIcon size="scale" isfill={true} color="dimgray" />
      );
    }
  };

  render() {
    const { data, selectedKeys } = this.props;

    console.log("SettingsTreeMenu", this.props);

    return (
      <TreeMenu
        className="people-tree-menu"
        checkable={false}
        draggable={false}
        disabled={false}
        multiple={false}
        showIcon={true}
        defaultExpandAll={true}
        switcherIcon={this.switcherIcon}
        onSelect={this.onSelect}
        selectedKeys={selectedKeys}
      >
        {getItems(data)}
      </TreeMenu>
    );
  };
};

function mapStateToProps(state) {
  return {
    data: state.auth.settings.settingsTree.list,
    selectedKeys: state.auth.settings.settingsTree.selectedKey
  };
}

export default connect(mapStateToProps, { selectSettingNode })(ArticleBodyContent);